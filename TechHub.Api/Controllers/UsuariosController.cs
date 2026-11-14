using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Common;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;

namespace TechHub.Api.Controllers;

/// <summary>Cuentas del sistema. Solo administradores. Los compradores se registran solos desde la app.</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class UsuariosController(AppDbContext db, IPasswordHasher<Usuario> hasher) : ControllerBase
{
    internal static UsuarioDto ADto(Usuario u) =>
        new(u.Id, u.NombreUsuario, u.NombreCompleto, u.Email, u.Rol.Nombre, u.Activo, u.FechaCreacion, u.ClienteId);

    [HttpGet]
    public async Task<ActionResult<PagedResult<UsuarioDto>>> Listar([FromQuery] UsuarioQuery q, CancellationToken ct)
    {
        var query = db.Usuarios.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Rol))
            query = query.Where(u => u.Rol.Nombre == q.Rol);
        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(u => u.NombreUsuario.Contains(b) || u.NombreCompleto.Contains(b) || u.Email.Contains(b));
        }

        return await query
            .OrderBy(u => u.RolId).ThenBy(u => u.NombreCompleto)
            .Select(u => new UsuarioDto(u.Id, u.NombreUsuario, u.NombreCompleto, u.Email, u.Rol.Nombre, u.Activo, u.FechaCreacion, u.ClienteId))
            .ToPagedResultAsync(q.Pagina, q.Tamanio, ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UsuarioDto>> Obtener(int id, CancellationToken ct)
    {
        var u = await db.Usuarios.AsNoTracking().Include(x => x.Rol).FirstOrDefaultAsync(x => x.Id == id, ct);
        return u is null ? NotFound() : Ok(ADto(u));
    }

    /// <summary>Crea un nuevo administrador.</summary>
    [HttpPost]
    [ProducesResponseType<UsuarioDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Crear(UsuarioCrearRequest req, CancellationToken ct)
    {
        var nombre = req.NombreUsuario.Trim();
        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.NombreUsuario == nombre || u.Email == email, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Duplicado", Detail = "Ya existe un usuario con ese nombre de usuario o email." });

        var usuario = new Usuario
        {
            NombreUsuario = nombre,
            NombreCompleto = req.NombreCompleto.Trim(),
            Email = email,
            RolId = Roles.AdminId
        };
        usuario.PasswordHash = hasher.HashPassword(usuario, req.Password);

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);
        await db.Entry(usuario).Reference(u => u.Rol).LoadAsync(ct);
        return CreatedAtAction(nameof(Obtener), new { id = usuario.Id }, ADto(usuario));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UsuarioDto>> Actualizar(int id, UsuarioActualizarRequest req, CancellationToken ct)
    {
        var usuario = await db.Usuarios.Include(u => u.Cliente).Include(u => u.Rol).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return NotFound();

        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.Id != id && (u.Email == email || u.NombreUsuario == email), ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Duplicado", Detail = "Ese email ya está en uso." });

        // Evita que el sistema se quede sin administradores activos.
        if (usuario.RolId == Roles.AdminId && usuario.Activo && !req.Activo
            && !await db.Usuarios.AnyAsync(u => u.Id != id && u.RolId == Roles.AdminId && u.Activo, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Operación no permitida", Detail = "Debe quedar al menos un administrador activo." });

        usuario.NombreCompleto = req.NombreCompleto.Trim();
        usuario.Activo = req.Activo;
        if (usuario.RolId == Roles.CompradorId)
        {
            // El comprador inicia sesión con su email: se mantienen sincronizados.
            usuario.Email = email;
            usuario.NombreUsuario = email;
            if (usuario.Cliente is not null)
            {
                usuario.Cliente.Email = email;
                usuario.Cliente.Nombre = usuario.NombreCompleto;
            }
        }
        else
        {
            usuario.Email = email;
        }

        if (!string.IsNullOrWhiteSpace(req.NuevaPassword))
            usuario.PasswordHash = hasher.HashPassword(usuario, req.NuevaPassword);

        await db.SaveChangesAsync(ct);
        return Ok(ADto(usuario));
    }

    /// <summary>Baja lógica: la cuenta queda inactiva y no puede iniciar sesión, pero su historial se conserva.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return NotFound();

        if (usuario.Id == User.ObtenerUsuarioId())
            return Conflict(new ProblemDetails { Status = 409, Title = "Operación no permitida", Detail = "No podés darte de baja a vos mismo." });

        if (usuario.RolId == Roles.AdminId && !await db.Usuarios.AnyAsync(u => u.Id != id && u.RolId == Roles.AdminId && u.Activo, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Operación no permitida", Detail = "Debe quedar al menos un administrador activo." });

        usuario.Activo = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
