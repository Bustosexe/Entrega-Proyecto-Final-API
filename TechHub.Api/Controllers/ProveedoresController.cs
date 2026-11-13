using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Common;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;

namespace TechHub.Api.Controllers;

[ApiController]
[Route("api/proveedores")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class ProveedoresController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProveedorDto>>> Listar([FromQuery] PaginacionQuery q, CancellationToken ct)
    {
        var query = db.Proveedores.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(p => p.RazonSocial.Contains(b) || p.Cuit.Contains(b));
        }

        return await query
            .OrderBy(p => p.RazonSocial)
            .Select(p => new ProveedorDto(p.Id, p.RazonSocial, p.Cuit, p.Email, p.Telefono, p.Direccion, p.Activo))
            .ToPagedResultAsync(q.Pagina, q.Tamanio, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProveedorDto>> Obtener(int id, CancellationToken ct)
    {
        var dto = await db.Proveedores.AsNoTracking().Where(p => p.Id == id)
            .Select(p => new ProveedorDto(p.Id, p.RazonSocial, p.Cuit, p.Email, p.Telefono, p.Direccion, p.Activo))
            .FirstOrDefaultAsync(ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [ProducesResponseType<ProveedorDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProveedorDto>> Crear(ProveedorRequest req, CancellationToken ct)
    {
        var cuit = req.Cuit.Trim();
        if (await db.Proveedores.AnyAsync(p => p.Cuit == cuit, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Duplicado", Detail = "Ya existe un proveedor con ese CUIT." });

        var p = new Proveedor
        {
            RazonSocial = req.RazonSocial.Trim(),
            Cuit = cuit,
            Email = req.Email?.Trim(),
            Telefono = req.Telefono?.Trim(),
            Direccion = req.Direccion?.Trim(),
            Activo = req.Activo
        };
        db.Proveedores.Add(p);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Obtener), new { id = p.Id },
            new ProveedorDto(p.Id, p.RazonSocial, p.Cuit, p.Email, p.Telefono, p.Direccion, p.Activo));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, ProveedorRequest req, CancellationToken ct)
    {
        var p = await db.Proveedores.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();

        var cuit = req.Cuit.Trim();
        if (await db.Proveedores.AnyAsync(x => x.Id != id && x.Cuit == cuit, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Duplicado", Detail = "Ya existe un proveedor con ese CUIT." });

        p.RazonSocial = req.RazonSocial.Trim();
        p.Cuit = cuit;
        p.Email = req.Email?.Trim();
        p.Telefono = req.Telefono?.Trim();
        p.Direccion = req.Direccion?.Trim();
        p.Activo = req.Activo;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Baja lógica (se conserva el historial de compras).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var p = await db.Proveedores.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();
        p.Activo = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
