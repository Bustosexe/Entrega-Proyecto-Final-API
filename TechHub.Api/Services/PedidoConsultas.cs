using Microsoft.EntityFrameworkCore;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;

namespace TechHub.Api.Services;

/// <summary>
/// Arma el PedidoDto completo (cabecera + renglones + historial) a partir de las tablas
/// Pedidos, DetallesPedido, HistorialPedidos, Clientes, Productos, EstadosPedido y Usuarios.
/// Lo usan los pedidos, los reportes y las notificaciones.
/// </summary>
public static class PedidoConsultas
{
    public static async Task<PedidoDto?> ObtenerPedidoAsync(this AppDbContext db, string codigo, CancellationToken ct = default)
    {
        var pedidos = await db.CargarPedidosAsync([codigo], ct);
        return pedidos.GetValueOrDefault(codigo);
    }

    public static async Task<Dictionary<string, PedidoDto>> CargarPedidosAsync(this AppDbContext db, IReadOnlyCollection<string> codigos,
        CancellationToken ct = default)
    {
        if (codigos.Count == 0) return new Dictionary<string, PedidoDto>();

        var filas = await db.Pedidos.AsNoTracking()
            .Where(p => codigos.Contains(p.Codigo))
            .Select(p => new
            {
                p.Codigo, p.Fecha, p.ClienteId, p.Observaciones, p.ComprobanteArchivo,
                Estado = p.Estado.Codigo, EstadoTexto = p.Estado.Nombre,
                Cliente = p.Cliente.Nombre, p.Cliente.Telefono, p.Cliente.Direccion,
                Items = p.Detalles
                    .OrderBy(d => d.Id)
                    .Select(d => new PedidoItemDto(d.Id, d.ProductoId, d.Producto.Nombre, d.Producto.ImagenUrl,
                        d.Cantidad, d.PrecioUnitario, d.Cantidad * d.PrecioUnitario))
                    .ToList(),
                Historial = p.Historial
                    .OrderBy(h => h.Fecha).ThenBy(h => h.Id)
                    .Select(h => new HistorialPedidoDto(h.Fecha, h.Estado.Codigo, h.Estado.Nombre,
                        h.Usuario != null ? h.Usuario.NombreCompleto : null,
                        h.Usuario != null && h.Usuario.RolId == Roles.AdminId,
                        h.Comentario))
                    .ToList()
            })
            .AsSplitQuery()
            .ToListAsync(ct);

        return filas.ToDictionary(f => f.Codigo, f =>
        {
            // Fecha del comprobante = último paso a "Pago en revisión".
            var ultimoEnvio = f.Historial.LastOrDefault(h => h.Estado == EstadosPedido.EnRevision);
            // Motivo de rechazo: si el pedido volvió a "esperando pago", el comentario de ese último cambio.
            var ultimo = f.Historial.LastOrDefault();
            var motivo = f.Estado == EstadosPedido.Pendiente && ultimo is { Estado: EstadosPedido.Pendiente, Comentario: not null }
                ? ultimo.Comentario
                : null;

            return new PedidoDto(
                f.Codigo, f.Fecha, f.Estado, f.EstadoTexto,
                f.ClienteId, f.Cliente, f.Telefono, f.Direccion,
                f.Items.Sum(i => i.Subtotal), f.Items.Sum(i => i.Cantidad), f.Observaciones,
                f.ComprobanteArchivo is not null, f.ComprobanteArchivo is not null ? ultimoEnvio?.Fecha : null, motivo,
                f.Items, f.Historial);
        });
    }
}
