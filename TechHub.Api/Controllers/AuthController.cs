using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Common;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;
using TechHub.Api.Services;

namespace TechHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(AppDbContext db, ITokenService tokens, IPasswordHasher<Usuario> hasher) : ControllerBase
{
    /// <summary>Inicia sesión con usuario o email y contraseña. Devuelve el Bearer Token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req, CancellationToken ct)
    {
        var login = req.Usuario.Trim();
        var usuario = await db.Usuarios
            .Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.NombreUsuario == login || u.Email == login, ct);

        // Mismo mensaje para usuario inexistente o contraseña incorrecta: no se revela cuál falló.
        if (usuario is null || !usuario.Activo)
            return Unauthorized(Problema("Usuario o contraseña incorrectos."));

        var resultado = hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, req.Password);
        if (resultado == PasswordVerificationResult.Failed)
            return Unauthorized(Problema("Usuario o contraseña incorrectos."));

        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.PasswordHash = hasher.HashPassword(usuario, req.Password);
            await db.SaveChangesAsync(ct);
        }

        return Ok(CrearRespuesta(usuario));
    }

    /// <summary>
    /// Registro de comprador desde la app. Crea la ficha de Cliente y la cuenta (rol Comprador) y devuelve el token.
    /// Si el DNI/CUIT ya estaba cargado como cliente del local y no tenía cuenta, se vincula.
    /// </summary>
    [HttpPost("registro")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoginResponse>> Registro(RegistroRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var documento = req.Documento.Trim();

        if (await db.Usuarios.AnyAsync(u => u.Email == email || u.NombreUsuario == email, ct))
            return Conflict(new ProblemDetails { Status = 409, Title = "Email registrado", Detail = "Ya existe una cuenta con ese email. Iniciá sesión." });

        var cliente = await db.Clientes.Include(c => c.Usuario).FirstOrDefaultAsync(c => c.Documento == documento, ct);
        if (cliente?.Usuario is not null)
            return Conflict(new ProblemDetails { Status = 409, Title = "DNI/CUIT registrado", Detail = "Ese DNI/CUIT ya tiene una cuenta asociada." });

        if (cliente is null)
        {
            cliente = new Cliente { Documento = documento };
            db.Clientes.Add(cliente);
        }
        cliente.Nombre = req.Nombre.Trim();
        cliente.Email = email;
        cliente.Telefono = req.Telefono?.Trim();
        cliente.Direccion = req.Direccion?.Trim();
        cliente.Activo = true;

        var usuario = new Usuario
        {
            NombreUsuario = email,
            NombreCompleto = cliente.Nombre,
            Email = email,
            RolId = Roles.CompradorId,
            Cliente = cliente
        };
        usuario.PasswordHash = hasher.HashPassword(usuario, req.Password);
        db.Usuarios.Add(usuario);

        await db.SaveChangesAsync(ct); // Cliente + Usuario en una sola transacción
        await db.Entry(usuario).Reference(u => u.Rol).LoadAsync(ct);

        return CreatedAtAction(nameof(Perfil), null, CrearRespuesta(usuario));
    }

    /// <summary>Datos del usuario logueado y, si es comprador, su ficha de cliente.</summary>
    [HttpGet("perfil")]
    [Authorize]
    [ProducesResponseType<PerfilDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PerfilDto>> Perfil(CancellationToken ct)
    {
        var id = User.ObtenerUsuarioId();
        var usuario = await db.Usuarios.AsNoTracking().Include(u => u.Cliente).Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        return usuario is null ? Unauthorized() : Ok(APerfil(usuario));
    }

    /// <summary>Actualiza nombre, teléfono y dirección; opcionalmente la contraseña (pidiendo la actual).</summary>
    [HttpPut("perfil")]
    [Authorize]
    public async Task<ActionResult<PerfilDto>> ActualizarPerfil(PerfilRequest req, CancellationToken ct)
    {
        var id = User.ObtenerUsuarioId();
        var usuario = await db.Usuarios.Include(u => u.Cliente).Include(u => u.Rol)
            .FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null) return Unauthorized();

        if (!string.IsNullOrWhiteSpace(req.NuevaPassword))
        {
            var ok = !string.IsNullOrEmpty(req.PasswordActual)
                     && hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, req.PasswordActual) != PasswordVerificationResult.Failed;
            if (!ok)
                return BadRequest(new ProblemDetails { Status = 400, Title = "Contraseña", Detail = "La contraseña actual no es correcta." });
            usuario.PasswordHash = hasher.HashPassword(usuario, req.NuevaPassword);
        }

        usuario.NombreCompleto = req.Nombre.Trim();
        if (usuario.Cliente is not null)
        {
            usuario.Cliente.Nombre = usuario.NombreCompleto;
            usuario.Cliente.Telefono = req.Telefono?.Trim();
            usuario.Cliente.Direccion = req.Direccion?.Trim();
        }

        await db.SaveChangesAsync(ct);
        return Ok(APerfil(usuario));
    }

    // ---------------------------------------------------------------------

    private LoginResponse CrearRespuesta(Usuario usuario)
    {
        var (token, expira) = tokens.GenerarToken(usuario);
        return new LoginResponse(token, expira, UsuariosController.ADto(usuario));
    }

    private static PerfilDto APerfil(Usuario u) => new(
        UsuariosController.ADto(u),
        u.Cliente is null ? null : ClientesController.ADto(u.Cliente));

    private static ProblemDetails Problema(string detalle) => new()
    {
        Status = StatusCodes.Status401Unauthorized,
        Title = "No autorizado",
        Detail = detalle
    };
}
