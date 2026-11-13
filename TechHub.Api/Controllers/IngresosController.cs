using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Common;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;
using TechHub.Api.Services;

namespace TechHub.Api.Controllers;

/// <summary>Compras a proveedores. Registrar un ingreso suma stock automáticamente.</summary>
[ApiController]
[Route("api/ingresos")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class IngresosController(AppDbContext db, IMovimientoService movimientos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<IngresoDto>>> Listar([FromQuery] MovimientoQuery q, CancellationToken ct)
    {
        var query = db.Ingresos.AsNoTracking();

        if (q.ProductoId is int productoId) query = query.Where(i => i.ProductoId == productoId);
        if (q.TerceroId is int proveedorId) query = query.Where(i => i.ProveedorId == proveedorId);
        if (q.Desde is DateTime desde) query = query.Where(i => i.Fecha >= desde);
        if (q.Hasta is DateTime hasta) query = query.Where(i => i.Fecha < hasta.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(i => i.Producto.Nombre.Contains(b) || i.Proveedor.RazonSocial.Contains(b)
                                     || (i.NumeroComprobante != null && i.NumeroComprobante.Contains(b)));
        }

        return await query
            .OrderByDescending(i => i.Fecha).ThenByDescending(i => i.Id)
            .Select(i => new IngresoDto(i.Id, i.Fecha, i.ProductoId, i.Producto.Nombre, i.ProveedorId, i.Proveedor.RazonSocial,
                i.Cantidad, i.CostoUnitario, i.Total, i.NumeroComprobante, i.Observaciones, i.Usuario.NombreUsuario))
            .ToPagedResultAsync(q.Pagina, q.Tamanio, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<IngresoDto>> Obtener(int id, CancellationToken ct)
    {
        var dto = await ProyectarAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Registra la llegada de mercadería de un proveedor y suma el stock (solo Admin).</summary>
    [HttpPost]
    [ProducesResponseType<IngresoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IngresoDto>> Registrar(IngresoRequest req, CancellationToken ct)
    {
        var ingreso = await movimientos.RegistrarIngresoAsync(
            req.ProductoId, req.ProveedorId, User.ObtenerUsuarioId(), req.Cantidad, req.CostoUnitario,
            req.NumeroComprobante?.Trim(), req.Observaciones?.Trim(), ct);

        return CreatedAtAction(nameof(Obtener), new { id = ingreso.Id }, await ProyectarAsync(ingreso.Id, ct));
    }

    private Task<IngresoDto?> ProyectarAsync(int id, CancellationToken ct) =>
        db.Ingresos.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new IngresoDto(i.Id, i.Fecha, i.ProductoId, i.Producto.Nombre, i.ProveedorId, i.Proveedor.RazonSocial,
                i.Cantidad, i.CostoUnitario, i.Total, i.NumeroComprobante, i.Observaciones, i.Usuario.NombreUsuario))
            .FirstOrDefaultAsync(ct);
}
