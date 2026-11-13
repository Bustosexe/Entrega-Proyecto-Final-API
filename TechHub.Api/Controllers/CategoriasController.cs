using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Common;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;

namespace TechHub.Api.Controllers;

[ApiController]
[Route("api/categorias")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class CategoriasController(AppDbContext db) : ControllerBase
{
    /// <summary>Listado paginado. Público: lo usa el catálogo.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<CategoriaDto>>> Listar([FromQuery] PaginacionQuery q, CancellationToken ct)
    {
        var query = db.Categorias.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(c => c.Nombre.Contains(b));
        }

        return await query
            .OrderBy(c => c.Nombre)
            .Select(c => new CategoriaDto(c.Id, c.Nombre, c.Descripcion, c.Activo, c.Productos.Count(p => p.Activo)))
            .ToPagedResultAsync(q.Pagina, q.Tamanio, ct);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<CategoriaDto>> Obtener(int id, CancellationToken ct)
    {
        var dto = await db.Categorias.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoriaDto(c.Id, c.Nombre, c.Descripcion, c.Activo, c.Productos.Count(p => p.Activo)))
            .FirstOrDefaultAsync(ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [ProducesResponseType<CategoriaDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CategoriaDto>> Crear(CategoriaRequest req, CancellationToken ct)
    {
        var nombre = req.Nombre.Trim();
        if (await db.Categorias.AnyAsync(c => c.Nombre == nombre, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Duplicado", Detail = "Ya existe una categoría con ese nombre." });

        var categoria = new Categoria { Nombre = nombre, Descripcion = req.Descripcion?.Trim(), Activo = req.Activo };
        db.Categorias.Add(categoria);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Obtener), new { id = categoria.Id },
            new CategoriaDto(categoria.Id, categoria.Nombre, categoria.Descripcion, categoria.Activo, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, CategoriaRequest req, CancellationToken ct)
    {
        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (categoria is null) return NotFound();

        var nombre = req.Nombre.Trim();
        if (await db.Categorias.AnyAsync(c => c.Id != id && c.Nombre == nombre, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Duplicado", Detail = "Ya existe una categoría con ese nombre." });

        categoria.Nombre = nombre;
        categoria.Descripcion = req.Descripcion?.Trim();
        categoria.Activo = req.Activo;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Elimina la categoría solo si no tiene productos asociados.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (categoria is null) return NotFound();

        if (await db.Productos.AnyAsync(p => p.CategoriaId == id, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Categoría en uso", Detail = "La categoría tiene productos. Desactivala en lugar de eliminarla." });

        db.Categorias.Remove(categoria);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
