using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Data;
using TechHub.Api.Models;

namespace TechHub.Api.Services;

/// <summary>Error de negocio que el manejador global traduce a 400 / 403 / 404 / 409.</summary>
public class ReglaNegocioException(string mensaje, int statusCode = StatusCodes.Status400BadRequest) : Exception(mensaje)
{
    public int StatusCode { get; } = statusCode;
}

public interface IMovimientoService
{
    Task<Ingreso> RegistrarIngresoAsync(int productoId, int proveedorId, int usuarioId, int cantidad,
        decimal costoUnitario, string? comprobante, string? observaciones, CancellationToken ct = default);

    /// <summary>Crea un pedido (una Salida por producto) reservando el stock. Devuelve el código del pedido.</summary>
    Task<string> RegistrarPedidoAsync(int clienteId, int usuarioId, IEnumerable<(int ProductoId, int Cantidad)> items,
        string estadoInicial, string? observaciones, CancellationToken ct = default);

    /// <summary>Cambia el estado de todo el pedido. Si se cancela, devuelve el stock.</summary>
    Task CambiarEstadoAsync(string codigoPedido, string nuevoEstado, CancellationToken ct = default);
}

/// <summary>
/// Lógica de stock. Cada movimiento se registra dentro de una transacción:
/// o se guardan el registro y el cambio de stock, o no se guarda nada.
/// </summary>
public class MovimientoService(AppDbContext db) : IMovimientoService
{
    public async Task<Ingreso> RegistrarIngresoAsync(int productoId, int proveedorId, int usuarioId, int cantidad,
        decimal costoUnitario, string? comprobante, string? observaciones, CancellationToken ct = default)
    {
        if (!await db.Proveedores.AnyAsync(p => p.Id == proveedorId && p.Activo, ct))
            throw new ReglaNegocioException("El proveedor no existe o está dado de baja.", StatusCodes.Status404NotFound);

        if (!await db.Productos.AnyAsync(p => p.Id == productoId, ct))
            throw new ReglaNegocioException("El producto no existe.", StatusCodes.Status404NotFound);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // UPDATE atómico en SQL: Stock = Stock + @cantidad
        await db.Productos
            .Where(p => p.Id == productoId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + cantidad), ct);

        var ingreso = new Ingreso
        {
            ProductoId = productoId,
            ProveedorId = proveedorId,
            UsuarioId = usuarioId,
            Cantidad = cantidad,
            CostoUnitario = costoUnitario,
            Total = cantidad * costoUnitario,
            NumeroComprobante = comprobante,
            Observaciones = observaciones,
            Fecha = DateTime.UtcNow
        };
        db.Ingresos.Add(ingreso);
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return ingreso;
    }

    public async Task<string> RegistrarPedidoAsync(int clienteId, int usuarioId, IEnumerable<(int ProductoId, int Cantidad)> items,
        string estadoInicial, string? observaciones, CancellationToken ct = default)
    {
        // Si el mismo producto viene dos veces en el carrito, se suman las cantidades.
        var lineas = items
            .GroupBy(i => i.ProductoId)
            .Select(g => (ProductoId: g.Key, Cantidad: g.Sum(x => x.Cantidad)))
            .ToList();

        if (lineas.Count == 0)
            throw new ReglaNegocioException("El pedido no tiene productos.");

        if (!await db.Clientes.AnyAsync(c => c.Id == clienteId && c.Activo, ct))
            throw new ReglaNegocioException("El cliente no existe o está dado de baja.", StatusCodes.Status404NotFound);

        var ids = lineas.Select(l => l.ProductoId).ToList();
        var productos = await db.Productos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Nombre, p.Precio, p.Stock, p.Activo })
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var l in lineas)
        {
            if (!productos.TryGetValue(l.ProductoId, out var p))
                throw new ReglaNegocioException($"El producto {l.ProductoId} no existe.", StatusCodes.Status404NotFound);
            if (!p.Activo)
                throw new ReglaNegocioException($"\"{p.Nombre}\" ya no está a la venta.");
        }

        var codigo = GenerarCodigo();
        var fecha = DateTime.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        foreach (var l in lineas)
        {
            var p = productos[l.ProductoId];
            var cantidad = l.Cantidad;

            // UPDATE condicional y atómico: solo descuenta si alcanza el stock.
            // Si dos personas compran la última unidad a la vez, una de las dos recibe 0 filas afectadas.
            var filas = await db.Productos
                .Where(x => x.Id == l.ProductoId && x.Stock >= cantidad)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Stock, x => x.Stock - cantidad), ct);

            if (filas == 0)
                throw new ReglaNegocioException(
                    $"Stock insuficiente para \"{p.Nombre}\". Disponible: {p.Stock}, solicitado: {cantidad}.",
                    StatusCodes.Status409Conflict);

            db.Salidas.Add(new Salida
            {
                CodigoPedido = codigo,
                Estado = estadoInicial,
                Fecha = fecha,
                ProductoId = l.ProductoId,
                ClienteId = clienteId,
                UsuarioId = usuarioId,
                Cantidad = cantidad,
                PrecioUnitario = p.Precio,          // precio congelado al momento de la compra
                Total = cantidad * p.Precio,
                Observaciones = observaciones
            });
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return codigo;
    }

    public async Task CambiarEstadoAsync(string codigoPedido, string nuevoEstado, CancellationToken ct = default)
    {
        var filas = await db.Salidas.Where(s => s.CodigoPedido == codigoPedido).ToListAsync(ct);
        if (filas.Count == 0)
            throw new ReglaNegocioException("El pedido no existe.", StatusCodes.Status404NotFound);

        var actual = filas[0].Estado;
        if (!EstadosPedido.TransicionValida(actual, nuevoEstado))
            throw new ReglaNegocioException($"No se puede pasar un pedido {actual} a {nuevoEstado}.", StatusCodes.Status409Conflict);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (nuevoEstado == EstadosPedido.Cancelado)
        {
            // Devuelve al stock lo que estaba reservado.
            foreach (var f in filas)
            {
                var cantidad = f.Cantidad;
                await db.Productos
                    .Where(p => p.Id == f.ProductoId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + cantidad), ct);
            }
        }

        foreach (var f in filas)
            f.Estado = nuevoEstado;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Código legible y único en la práctica: P + fecha + 6 caracteres aleatorios. Ej: P261113-4F2A9C</summary>
    private static string GenerarCodigo() =>
        $"P{DateTime.UtcNow:yyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3))}";
}
