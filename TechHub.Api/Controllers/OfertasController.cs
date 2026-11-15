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
/// Ofertas: descuento temporal sobre un producto. Mientras está vigente, el catálogo muestra el precio rebajado
/// y los pedidos se cobran con él. Solo administradores (los compradores ven el resultado en el catálogo).
/// </summary>
[ApiController]
[Route("api/ofertas")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class OfertasController(AppDbContext db) : ControllerBase
{
    /// <summary>Ofertas paginadas, con filtro por estado (Vigente, Programada, Vencida, Pausada) y búsqueda.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<OfertaDto>>> Listar([FromQuery] OfertaQuery q, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var query = db.Ofertas.AsNoTracking().Include(o => o.Producto).AsQueryable();

        switch (q.Estado)
        {
            case "Vigente": query = query.Where(o => o.Activa && o.Desde <= ahora && o.Hasta >= ahora); break;
            case "Programada": query = query.Where(o => o.Activa && o.Hasta >= ahora && o.Desde > ahora); break;
            case "Vencida": query = query.Where(o => o.Hasta < ahora); break;
            case "Pausada": query = query.Where(o => !o.Activa && o.Hasta >= ahora); break;
        }

        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(o => o.Producto.Nombre.Contains(b) || (o.Titulo != null && o.Titulo.Contains(b)));
        }

        var pagina = await query
            .OrderByDescending(o => o.Hasta).ThenByDescending(o => o.Id)
            .ToPagedResultAsync(q.Pagina, q.Tamanio, ct);

        return new PagedResult<OfertaDto>(
            pagina.Items.Select(o => ADto(o, ahora)).ToList(), pagina.Pagina, pagina.Tamanio, pagina.TotalItems, pagina.TotalPaginas);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OfertaDto>> Obtener(int id, CancellationToken ct)
    {
        var oferta = await db.Ofertas.AsNoTracking().Include(o => o.Producto).FirstOrDefaultAsync(o => o.Id == id, ct);
        return oferta is null ? NotFound() : Ok(ADto(oferta, DateTime.UtcNow));
    }

    [HttpPost]
    [ProducesResponseType<OfertaDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<OfertaDto>> Crear(OfertaRequest req, CancellationToken ct)
    {
        await ValidarAsync(req, ct);

        var oferta = new Oferta
        {
            ProductoId = req.ProductoId,
            Titulo = string.IsNullOrWhiteSpace(req.Titulo) ? null : req.Titulo.Trim(),
            Porcentaje = req.Porcentaje,
            Desde = Precios.InicioDiaUtc(req.Desde),
            Hasta = Precios.FinDiaUtc(req.Hasta),
            Activa = req.Activa
        };
        db.Ofertas.Add(oferta);
        await db.SaveChangesAsync(ct);

        await db.Entry(oferta).Reference(o => o.Producto).LoadAsync(ct);
        return CreatedAtAction(nameof(Obtener), new { id = oferta.Id }, ADto(oferta, DateTime.UtcNow));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<OfertaDto>> Actualizar(int id, OfertaRequest req, CancellationToken ct)
    {
        var oferta = await db.Ofertas.Include(o => o.Producto).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (oferta is null) return NotFound();

        await ValidarAsync(req, ct);

        if (oferta.ProductoId != req.ProductoId)
        {
            oferta.ProductoId = req.ProductoId;
            await db.Entry(oferta).Reference(o => o.Producto).LoadAsync(ct);
        }
        oferta.Titulo = string.IsNullOrWhiteSpace(req.Titulo) ? null : req.Titulo.Trim();
        oferta.Porcentaje = req.Porcentaje;
        oferta.Desde = Precios.InicioDiaUtc(req.Desde);
        oferta.Hasta = Precios.FinDiaUtc(req.Hasta);
        oferta.Activa = req.Activa;
        await db.SaveChangesAsync(ct);

        return Ok(ADto(oferta, DateTime.UtcNow));
    }

    /// <summary>Elimina la oferta. Los pedidos ya hechos conservan el precio al que se cobraron.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var oferta = await db.Ofertas.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (oferta is null) return NotFound();

        db.Ofertas.Remove(oferta);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---------------------------------------------------------------------

    private async Task ValidarAsync(OfertaRequest req, CancellationToken ct)
    {
        if (req.Hasta < req.Desde)
            throw new ReglaNegocioException("La fecha de fin no puede ser anterior a la de inicio.");
        if (!await db.Productos.AnyAsync(p => p.Id == req.ProductoId, ct))
            throw new ReglaNegocioException("El producto no existe.", StatusCodes.Status404NotFound);
    }

    private static OfertaDto ADto(Oferta o, DateTime ahora)
    {
        var estado = o.Hasta < ahora ? "Vencida"
            : !o.Activa ? "Pausada"
            : o.Desde > ahora ? "Programada"
            : "Vigente";

        return new OfertaDto(
            o.Id, o.ProductoId, o.Producto.Nombre, o.Producto.ImagenUrl, o.Producto.Precio,
            o.Titulo, o.Porcentaje, Precios.ConDescuento(o.Producto.Precio, o.Porcentaje),
            Precios.FechaArgentina(o.Desde), Precios.FechaArgentina(o.Hasta), o.Activa, estado);
    }
}
