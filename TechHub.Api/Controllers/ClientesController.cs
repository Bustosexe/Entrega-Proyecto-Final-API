using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Common;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;

namespace TechHub.Api.Controllers;

/// <summary>Fichas de clientes (los que se registran en la app y los cargados en el local). Solo Admin.</summary>
[ApiController]
[Route("api/clientes")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class ClientesController(AppDbContext db) : ControllerBase
{
    internal static ClienteDto ADto(Cliente c) =>
        new(c.Id, c.Nombre, c.Documento, c.Email, c.Telefono, c.Direccion, c.Activo);

    [HttpGet]
    public async Task<ActionResult<PagedResult<ClienteDto>>> Listar([FromQuery] PaginacionQuery q, CancellationToken ct)
    {
        var query = db.Clientes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(c => c.Nombre.Contains(b) || c.Documento.Contains(b) || (c.Email != null && c.Email.Contains(b)));
        }

        return await query
            .OrderBy(c => c.Nombre)
            .Select(c => new ClienteDto(c.Id, c.Nombre, c.Documento, c.Email, c.Telefono, c.Direccion, c.Activo))
            .ToPagedResultAsync(q.Pagina, q.Tamanio, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClienteDto>> Obtener(int id, CancellationToken ct)
    {
        var dto = await db.Clientes.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new ClienteDto(c.Id, c.Nombre, c.Documento, c.Email, c.Telefono, c.Direccion, c.Activo))
            .FirstOrDefaultAsync(ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ClienteDto>> Crear(ClienteRequest req, CancellationToken ct)
    {
        var doc = req.Documento.Trim();
        if (await db.Clientes.AnyAsync(c => c.Documento == doc, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Duplicado", Detail = "Ya existe un cliente con ese DNI/CUIT." });

        var c = new Cliente
        {
            Nombre = req.Nombre.Trim(),
            Documento = doc,
            Email = req.Email?.Trim(),
            Telefono = req.Telefono?.Trim(),
            Direccion = req.Direccion?.Trim(),
            Activo = req.Activo
        };
        db.Clientes.Add(c);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Obtener), new { id = c.Id },
            new ClienteDto(c.Id, c.Nombre, c.Documento, c.Email, c.Telefono, c.Direccion, c.Activo));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, ClienteRequest req, CancellationToken ct)
    {
        var c = await db.Clientes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();

        var doc = req.Documento.Trim();
        if (await db.Clientes.AnyAsync(x => x.Id != id && x.Documento == doc, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Duplicado", Detail = "Ya existe un cliente con ese DNI/CUIT." });

        c.Nombre = req.Nombre.Trim();
        c.Documento = doc;
        c.Email = req.Email?.Trim();
        c.Telefono = req.Telefono?.Trim();
        c.Direccion = req.Direccion?.Trim();
        c.Activo = req.Activo;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Baja lógica (se conserva el historial de compras del cliente).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var c = await db.Clientes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return NotFound();
        c.Activo = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
