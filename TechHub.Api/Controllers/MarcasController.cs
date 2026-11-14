using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Common;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;

namespace TechHub.Api.Controllers;

/// <summary>Marcas de los productos (AMD, Intel, ASUS...). Lectura pública (filtro del catálogo); ABM solo Admin.</summary>
[ApiController]
[Route("api/marcas")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class MarcasController(AppDbContext db) : ControllerBase
{
    /// <summary>Listado paginado con la cantidad de productos activos de cada marca.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<MarcaDto>>> Listar([FromQuery] PaginacionQuery q, CancellationToken ct)
    {
        var query = db.Marcas.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(m => m.Nombre.Contains(b));
        }

        return await query
            .OrderBy(m => m.Nombre)
            .Select(m => new MarcaDto(m.Id, m.Nombre, m.Activo, m.Productos.Count(p => p.Activo)))
            .ToPagedResultAsync(q.Pagina, q.Tamanio, ct);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<MarcaDto>> Obtener(int id, CancellationToken ct)
    {
        var dto = await db.Marcas.AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new MarcaDto(m.Id, m.Nombre, m.Activo, m.Productos.Count(p => p.Activo)))
            .FirstOrDefaultAsync(ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [ProducesResponseType<MarcaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MarcaDto>> Crear(MarcaRequest req, CancellationToken ct)
    {
        var nombre = req.Nombre.Trim();
        if (await db.Marcas.AnyAsync(m => m.Nombre == nombre, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Duplicado", Detail = "Ya existe una marca con ese nombre." });

        var marca = new Marca { Nombre = nombre, Activo = req.Activo };
        db.Marcas.Add(marca);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Obtener), new { id = marca.Id }, new MarcaDto(marca.Id, marca.Nombre, marca.Activo, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, MarcaRequest req, CancellationToken ct)
    {
        var marca = await db.Marcas.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (marca is null) return NotFound();

        var nombre = req.Nombre.Trim();
        if (await db.Marcas.AnyAsync(m => m.Id != id && m.Nombre == nombre, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Duplicado", Detail = "Ya existe una marca con ese nombre." });

        marca.Nombre = nombre;
        marca.Activo = req.Activo;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Elimina la marca solo si no tiene productos asociados.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var marca = await db.Marcas.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (marca is null) return NotFound();

        if (await db.Productos.AnyAsync(p => p.MarcaId == id, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Marca en uso", Detail = "La marca tiene productos. Desactivala en lugar de eliminarla." });

        db.Marcas.Remove(marca);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
