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
/// Bitácora de auditoría: quién hizo qué y cuándo. Solo lectura y solo para administradores
/// (el registro lo genera automáticamente el filtro de auditoría en cada alta, modificación o baja).
/// </summary>
[ApiController]
[Route("api/auditoria")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class AuditoriaController(AppDbContext db) : ControllerBase
{
    /// <summary>Registros paginados (más recientes primero) con filtro por entidad, usuario, fechas y búsqueda.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditoriaDto>>> Listar([FromQuery] AuditoriaQuery q, CancellationToken ct)
    {
        var query = db.Auditoria.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.Entidad)) query = query.Where(a => a.Entidad == q.Entidad);
        if (q.UsuarioId is int usuarioId) query = query.Where(a => a.UsuarioId == usuarioId);
        // Las fechas se interpretan como días de Argentina.
        if (q.Desde is DateTime desde) { var d = Precios.InicioDiaUtc(DateOnly.FromDateTime(desde)); query = query.Where(a => a.Fecha >= d); }
        if (q.Hasta is DateTime hasta) { var h = Precios.FinDiaUtc(DateOnly.FromDateTime(hasta)); query = query.Where(a => a.Fecha <= h); }

        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(a => a.Accion.Contains(b) || a.Entidad.Contains(b)
                                     || (a.EntidadId != null && a.EntidadId.Contains(b))
                                     || (a.Detalle != null && a.Detalle.Contains(b))
                                     || (a.Usuario != null && a.Usuario.NombreCompleto.Contains(b)));
        }

        return await query
            .OrderByDescending(a => a.Fecha).ThenByDescending(a => a.Id)
            .Select(a => new AuditoriaDto(
                a.Id, a.Fecha,
                a.Usuario != null ? a.Usuario.NombreCompleto : null,
                a.Usuario != null ? a.Usuario.Rol.Nombre : null,
                a.Accion, a.Entidad, a.EntidadId, a.Detalle, a.Ip))
            .ToPagedResultAsync(q.Pagina, q.Tamanio, ct);
    }

    /// <summary>Entidades que tienen registros (para armar los filtros de la pantalla).</summary>
    [HttpGet("entidades")]
    public async Task<ActionResult<List<string>>> Entidades(CancellationToken ct) =>
        await db.Auditoria.AsNoTracking().Select(a => a.Entidad).Distinct().OrderBy(e => e).ToListAsync(ct);
}
