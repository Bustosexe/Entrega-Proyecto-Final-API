using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;

namespace TechHub.Api.Controllers;

/// <summary>Tablas de referencia de solo lectura: roles de usuario y estados de pedido.</summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class ReferenciasController(AppDbContext db) : ControllerBase
{
    /// <summary>Roles del sistema con la cantidad de usuarios de cada uno. Solo Admin.</summary>
    [HttpGet("roles")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<List<RolDto>>> ListarRoles(CancellationToken ct) =>
        await db.Roles.AsNoTracking()
            .OrderBy(r => r.Id)
            .Select(r => new RolDto(r.Id, r.Nombre, r.Descripcion, r.Usuarios.Count))
            .ToListAsync(ct);

    /// <summary>Estados posibles de un pedido, en el orden del circuito.</summary>
    [HttpGet("estados-pedido")]
    [AllowAnonymous]
    public async Task<ActionResult<List<EstadoPedidoDto>>> ListarEstadosPedido(CancellationToken ct) =>
        await db.EstadosPedido.AsNoTracking()
            .OrderBy(e => e.Id)
            .Select(e => new EstadoPedidoDto(e.Id, e.Codigo, e.Nombre))
            .ToListAsync(ct);
}
