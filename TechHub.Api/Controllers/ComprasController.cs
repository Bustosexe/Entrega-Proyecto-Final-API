using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Common;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;
using TechHub.Api.Services;

namespace TechHub.Api.Controllers;

/// <summary>
/// Compras a proveedores: una compra (factura) con uno o varios productos.
/// Cada renglón suma stock al producto. Solo administradores.
/// </summary>
[ApiController]
[Route("api/compras")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class ComprasController(AppDbContext db, IMovimientoService movimientos) : ControllerBase
{
    /// <summary>Compras paginadas (más recientes primero) con filtros por proveedor, producto, fechas y búsqueda.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CompraDto>>> Listar([FromQuery] CompraQuery q, CancellationToken ct)
    {
        var query = db.Compras.AsNoTracking().AsQueryable();

        if (q.ProveedorId is int proveedorId) query = query.Where(c => c.ProveedorId == proveedorId);
        if (q.ProductoId is int productoId) query = query.Where(c => c.Detalles.Any(d => d.ProductoId == productoId));
        if (q.Desde is DateTime desde) query = query.Where(c => c.Fecha >= desde);
        if (q.Hasta is DateTime hasta) query = query.Where(c => c.Fecha < hasta.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(c => (c.NumeroComprobante != null && c.NumeroComprobante.Contains(b))
                                     || c.Proveedor.RazonSocial.Contains(b)
                                     || c.Detalles.Any(d => d.Producto.Nombre.Contains(b)));
        }

        return await Proyectar(query.OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Id))
            .ToPagedResultAsync(q.Pagina, q.Tamanio, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CompraDto>> Obtener(int id, CancellationToken ct)
    {
        var dto = await Proyectar(db.Compras.AsNoTracking().Where(c => c.Id == id)).FirstOrDefaultAsync(ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Registra la compra (una factura con varios productos) y suma el stock, todo en una transacción.
    /// Devuelve 409 si esa factura del proveedor ya estaba cargada.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<CompraDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompraDto>> Crear(CompraRequest req, CancellationToken ct)
    {
        var compra = await movimientos.RegistrarCompraAsync(
            req.ProveedorId,
            User.ObtenerUsuarioId(),
            req.NumeroComprobante,
            req.Observaciones?.Trim(),
            req.Items.Select(i => (i.ProductoId, i.Cantidad, i.CostoUnitario)),
            ct);

        var dto = await Proyectar(db.Compras.AsNoTracking().Where(c => c.Id == compra.Id)).FirstAsync(ct);
        return CreatedAtAction(nameof(Obtener), new { id = compra.Id }, dto);
    }

    private static IQueryable<CompraDto> Proyectar(IQueryable<Compra> query) =>
        query.Select(c => new CompraDto(
            c.Id,
            c.Fecha,
            c.ProveedorId,
            c.Proveedor.RazonSocial,
            c.NumeroComprobante,
            c.Observaciones,
            c.Usuario.NombreCompleto,
            c.Detalles.Sum(d => d.Cantidad),
            c.Detalles.Sum(d => d.Cantidad * d.CostoUnitario),
            c.Detalles
                .OrderBy(d => d.Id)
                .Select(d => new DetalleCompraDto(d.Id, d.ProductoId, d.Producto.Nombre, d.Producto.ImagenUrl,
                    d.Cantidad, d.CostoUnitario, d.Cantidad * d.CostoUnitario))
                .ToList()));
}
