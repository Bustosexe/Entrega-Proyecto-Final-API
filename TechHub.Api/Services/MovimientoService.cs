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
    /// <summary>Registra una compra a un proveedor (cabecera + renglones) y suma el stock de cada producto.</summary>
    Task<Compra> RegistrarCompraAsync(int proveedorId, int usuarioId, string? numeroComprobante, string? observaciones,
        IEnumerable<(int ProductoId, int Cantidad, decimal CostoUnitario)> items, CancellationToken ct = default);

    /// <summary>
    /// Crea un pedido (cabecera + renglones) reservando el stock. Devuelve el código del pedido.
    /// Cada producto se cobra con su oferta vigente; los combos aportan sus productos con el descuento del combo
    /// (se aplica el mejor precio entre el del combo y la oferta del producto).
    /// </summary>
    Task<string> RegistrarPedidoAsync(int clienteId, int usuarioId, IEnumerable<(int ProductoId, int Cantidad)> items,
        string estadoInicial, string? observaciones, IEnumerable<(int ComboId, int Cantidad)>? combos = null,
        CancellationToken ct = default);

    /// <summary>
    /// Cambia el estado del pedido y lo registra en el historial (quién, cuándo y por qué).
    /// Si se cancela, devuelve el stock.
    /// </summary>
    Task CambiarEstadoAsync(string codigoPedido, string nuevoEstado, int usuarioId, string? comentario = null,
        CancellationToken ct = default);
}

/// <summary>
/// Lógica de stock. Cada movimiento se registra dentro de una transacción:
/// o se guardan los registros y el cambio de stock, o no se guarda nada.
/// </summary>
public class MovimientoService(AppDbContext db) : IMovimientoService
{
    public async Task<Compra> RegistrarCompraAsync(int proveedorId, int usuarioId, string? numeroComprobante, string? observaciones,
        IEnumerable<(int ProductoId, int Cantidad, decimal CostoUnitario)> items, CancellationToken ct = default)
    {
        // Si el mismo producto viene dos veces, se suman las cantidades (con el último costo informado).
        var lineas = items
            .GroupBy(i => i.ProductoId)
            .Select(g => (ProductoId: g.Key, Cantidad: g.Sum(x => x.Cantidad), CostoUnitario: g.Last().CostoUnitario))
            .ToList();

        if (lineas.Count == 0)
            throw new ReglaNegocioException("La compra no tiene productos.");

        if (!await db.Proveedores.AnyAsync(p => p.Id == proveedorId && p.Activo, ct))
            throw new ReglaNegocioException("El proveedor no existe o está dado de baja.", StatusCodes.Status404NotFound);

        var numero = string.IsNullOrWhiteSpace(numeroComprobante) ? null : numeroComprobante.Trim();
        if (numero is not null && await db.Compras.AnyAsync(c => c.ProveedorId == proveedorId && c.NumeroComprobante == numero, ct))
            throw new ReglaNegocioException($"La factura {numero} de este proveedor ya fue cargada.", StatusCodes.Status409Conflict);

        var ids = lineas.Select(l => l.ProductoId).ToList();
        var existentes = await db.Productos.Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        var faltante = ids.Except(existentes).FirstOrDefault();
        if (faltante != 0)
            throw new ReglaNegocioException($"El producto {faltante} no existe.", StatusCodes.Status404NotFound);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        foreach (var l in lineas)
        {
            var cantidad = l.Cantidad;
            // UPDATE atómico en SQL: Stock = Stock + @cantidad
            await db.Productos
                .Where(p => p.Id == l.ProductoId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + cantidad), ct);
        }

        var compra = new Compra
        {
            ProveedorId = proveedorId,
            UsuarioId = usuarioId,
            NumeroComprobante = numero,
            Observaciones = observaciones,
            Fecha = DateTime.UtcNow,
            Detalles = lineas.Select(l => new DetalleCompra
            {
                ProductoId = l.ProductoId,
                Cantidad = l.Cantidad,
                CostoUnitario = l.CostoUnitario
            }).ToList()
        };
        db.Compras.Add(compra);
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return compra;
    }

    public async Task<string> RegistrarPedidoAsync(int clienteId, int usuarioId, IEnumerable<(int ProductoId, int Cantidad)> items,
        string estadoInicial, string? observaciones, IEnumerable<(int ComboId, int Cantidad)>? combos = null,
        CancellationToken ct = default)
    {
        // Si el mismo producto (o combo) viene dos veces en el carrito, se suman las cantidades.
        var sueltos = items
            .GroupBy(i => i.ProductoId)
            .Select(g => (ProductoId: g.Key, Cantidad: g.Sum(x => x.Cantidad)))
            .ToList();
        var combosPedidos = (combos ?? Array.Empty<(int ComboId, int Cantidad)>())
            .GroupBy(c => c.ComboId)
            .Select(g => (ComboId: g.Key, Cantidad: g.Sum(x => x.Cantidad)))
            .ToList();

        if (sueltos.Count == 0 && combosPedidos.Count == 0)
            throw new ReglaNegocioException("El pedido no tiene productos.");

        if (!await db.Clientes.AnyAsync(c => c.Id == clienteId && c.Activo, ct))
            throw new ReglaNegocioException("El cliente no existe o está dado de baja.", StatusCodes.Status404NotFound);

        // Combos pedidos: deben existir y estar activos.
        var comboIds = combosPedidos.Select(c => c.ComboId).ToList();
        var combosDb = await db.Combos.AsNoTracking()
            .Include(c => c.Items)
            .Where(c => comboIds.Contains(c.Id))
            .ToListAsync(ct);
        foreach (var c in combosPedidos)
        {
            var combo = combosDb.FirstOrDefault(x => x.Id == c.ComboId)
                ?? throw new ReglaNegocioException($"El combo {c.ComboId} no existe.", StatusCodes.Status404NotFound);
            if (!combo.Activo)
                throw new ReglaNegocioException($"El combo \"{combo.Nombre}\" ya no está a la venta.");
        }

        var ids = sueltos.Select(s => s.ProductoId)
            .Concat(combosDb.SelectMany(c => c.Items.Select(i => i.ProductoId)))
            .Distinct()
            .ToList();

        var productos = await db.Productos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Nombre, p.Precio, p.Stock, p.Activo })
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var id in ids)
        {
            if (!productos.TryGetValue(id, out var p))
                throw new ReglaNegocioException($"El producto {id} no existe.", StatusCodes.Status404NotFound);
            if (!p.Activo)
                throw new ReglaNegocioException($"\"{p.Nombre}\" ya no está a la venta.");
        }

        // Mayor descuento de las ofertas vigentes en este momento, por producto.
        var ahora = DateTime.UtcNow;
        var ofertas = await db.Ofertas.AsNoTracking()
            .Where(o => ids.Contains(o.ProductoId) && o.Activa && o.Desde <= ahora && o.Hasta >= ahora)
            .GroupBy(o => o.ProductoId)
            .Select(g => new { ProductoId = g.Key, Pct = g.Max(o => o.Porcentaje) })
            .ToDictionaryAsync(o => o.ProductoId, o => o.Pct, ct);

        decimal PrecioConOferta(int productoId) =>
            ofertas.TryGetValue(productoId, out var pct)
                ? Precios.ConDescuento(productos[productoId].Precio, pct)
                : productos[productoId].Precio;

        // Un producto es un solo renglón del pedido, aunque venga suelto y dentro de un combo:
        // se suman las cantidades y el precio unitario es el promedio ponderado.
        var renglones = new Dictionary<int, (int Cantidad, decimal Total)>();
        void Sumar(int productoId, int cantidad, decimal unitario)
        {
            renglones.TryGetValue(productoId, out var r);
            renglones[productoId] = (r.Cantidad + cantidad, r.Total + unitario * cantidad);
        }

        foreach (var s in sueltos)
            Sumar(s.ProductoId, s.Cantidad, PrecioConOferta(s.ProductoId));

        foreach (var c in combosPedidos)
        {
            var combo = combosDb.First(x => x.Id == c.ComboId);
            foreach (var it in combo.Items)
            {
                var precioCombo = Precios.ConDescuento(productos[it.ProductoId].Precio, combo.Porcentaje);
                Sumar(it.ProductoId, it.Cantidad * c.Cantidad, Math.Min(precioCombo, PrecioConOferta(it.ProductoId)));
            }
        }

        var lineas = renglones
            .Select(kv => (
                ProductoId: kv.Key,
                Cantidad: kv.Value.Cantidad,
                Precio: Math.Round(kv.Value.Total / kv.Value.Cantidad, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        // Los combos quedan anotados en las observaciones del pedido.
        var textoCombos = combosPedidos.Count == 0
            ? null
            : "Combos: " + string.Join(", ", combosPedidos.Select(c => $"{combosDb.First(x => x.Id == c.ComboId).Nombre} x{c.Cantidad}"));
        var notas = string.Join(" · ", new[] { observaciones, textoCombos }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (notas.Length > 500) notas = notas[..499] + "…";

        var estadoId = EstadosPedido.Id(estadoInicial);
        var pedido = new Pedido
        {
            Codigo = GenerarCodigo(),
            Fecha = ahora,
            ClienteId = clienteId,
            UsuarioId = usuarioId,
            EstadoId = estadoId,
            Observaciones = notas.Length == 0 ? null : notas
        };

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

            pedido.Detalles.Add(new DetallePedido
            {
                ProductoId = l.ProductoId,
                Cantidad = cantidad,
                PrecioUnitario = l.Precio   // precio congelado al momento de la compra (con oferta o combo)
            });
        }

        pedido.Historial.Add(new HistorialPedido { EstadoId = estadoId, Fecha = ahora, UsuarioId = usuarioId });

        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return pedido.Codigo;
    }

    public async Task CambiarEstadoAsync(string codigoPedido, string nuevoEstado, int usuarioId, string? comentario = null,
        CancellationToken ct = default)
    {
        var pedido = await db.Pedidos
            .Include(p => p.Detalles)
            .FirstOrDefaultAsync(p => p.Codigo == codigoPedido, ct)
            ?? throw new ReglaNegocioException("El pedido no existe.", StatusCodes.Status404NotFound);

        var actual = EstadosPedido.Codigo(pedido.EstadoId);
        if (!EstadosPedido.TransicionValida(actual, nuevoEstado))
            throw new ReglaNegocioException(
                $"No se puede pasar un pedido de \"{EstadosPedido.Texto(actual)}\" a \"{EstadosPedido.Texto(nuevoEstado)}\".",
                StatusCodes.Status409Conflict);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (nuevoEstado == EstadosPedido.Cancelado)
        {
            // Devuelve al stock lo que estaba reservado.
            foreach (var d in pedido.Detalles)
            {
                var cantidad = d.Cantidad;
                await db.Productos
                    .Where(p => p.Id == d.ProductoId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + cantidad), ct);
            }
        }

        var estadoId = EstadosPedido.Id(nuevoEstado);
        pedido.EstadoId = estadoId;
        db.HistorialPedidos.Add(new HistorialPedido
        {
            PedidoId = pedido.Id,
            EstadoId = estadoId,
            Fecha = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim()
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Código legible y único en la práctica: P + fecha + 6 caracteres aleatorios. Ej: P261113-4F2A9C</summary>
    private static string GenerarCodigo() =>
        $"P{DateTime.UtcNow:yyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3))}";
}
