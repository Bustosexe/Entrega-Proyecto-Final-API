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
/// Combos: paquetes de productos con un descuento por llevarlos juntos. El administrador decide qué incluir
/// (una PC completa, algunos componentes, etc.): no hay restricciones de compatibilidad.
/// El catálogo es público; solo los administradores crean y modifican combos.
/// </summary>
[ApiController]
[Route("api/combos")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class CombosController(AppDbContext db) : ControllerBase
{
    /// <summary>Combos paginados. Público para los activos; el administrador puede pedir también los dados de baja.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ComboDto>>> Listar([FromQuery] ComboQuery q, CancellationToken ct)
    {
        var query = db.Combos.AsNoTracking().Include(c => c.Items).AsQueryable();

        if (!(q.IncluirInactivos && User.IsInRole(Roles.Admin)))
            query = query.Where(c => c.Activo);

        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(c => c.Nombre.Contains(b));
        }

        var pagina = await query.OrderBy(c => c.Nombre).ThenBy(c => c.Id).ToPagedResultAsync(q.Pagina, q.Tamanio, ct);
        var dtos = await ADtosAsync(pagina.Items, ct);
        return new PagedResult<ComboDto>(dtos, pagina.Pagina, pagina.Tamanio, pagina.TotalItems, pagina.TotalPaginas);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ComboDto>> Obtener(int id, CancellationToken ct)
    {
        var dto = await DtoAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Pares de categorías que pueden ir juntas en un combo.</summary>
    [HttpGet("compatibilidad")]
    [AllowAnonymous]
    public async Task<ActionResult<List<CompatibilidadDto>>> Compatibilidad(CancellationToken ct) =>
        await db.CategoriasCompatibles.AsNoTracking()
            .OrderBy(c => c.CategoriaA.Nombre).ThenBy(c => c.CategoriaB.Nombre)
            .Select(c => new CompatibilidadDto(c.CategoriaAId, c.CategoriaA.Nombre, c.CategoriaBId, c.CategoriaB.Nombre))
            .ToListAsync(ct);

    [HttpPost]
    [ProducesResponseType<ComboDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ComboDto>> Crear(ComboRequest req, CancellationToken ct)
    {
        await ValidarAsync(req, null, ct);

        var combo = new Combo
        {
            Nombre = req.Nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Porcentaje = req.Porcentaje,
            Activo = req.Activo,
            Items = req.Items.Select(i => new ComboItem { ProductoId = i.ProductoId, Cantidad = i.Cantidad }).ToList()
        };
        db.Combos.Add(combo);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Obtener), new { id = combo.Id }, await DtoAsync(combo.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ComboDto>> Actualizar(int id, ComboRequest req, CancellationToken ct)
    {
        var combo = await db.Combos.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (combo is null) return NotFound();

        await ValidarAsync(req, id, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Se reemplazan los productos del combo (evita choques con el índice único ComboId + ProductoId).
        await db.ComboItems.Where(i => i.ComboId == id).ExecuteDeleteAsync(ct);

        combo.Nombre = req.Nombre.Trim();
        combo.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        combo.Porcentaje = req.Porcentaje;
        combo.Activo = req.Activo;
        foreach (var i in req.Items)
            db.ComboItems.Add(new ComboItem { ComboId = id, ProductoId = i.ProductoId, Cantidad = i.Cantidad });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(await DtoAsync(id, ct));
    }

    /// <summary>Baja lógica: el combo deja de verse en el catálogo pero los pedidos que lo incluyen se conservan.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var combo = await db.Combos.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (combo is null) return NotFound();

        combo.Activo = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---------------------------------------------------------------------

    /// <summary>
    /// Reglas del combo: nombre único, productos existentes y activos sin repetir y al menos dos productos.
    /// </summary>
    private async Task ValidarAsync(ComboRequest req, int? excluirId, CancellationToken ct)
    {
        var nombre = req.Nombre.Trim();
        if (await db.Combos.AnyAsync(c => c.Nombre == nombre && c.Id != excluirId, ct))
            throw new ReglaNegocioException("Ya existe un combo con ese nombre.", StatusCodes.Status409Conflict);

        var ids = req.Items.Select(i => i.ProductoId).ToList();
        if (ids.Distinct().Count() != ids.Count)
            throw new ReglaNegocioException("Un producto no puede repetirse en el combo: usá la cantidad.");

        var productos = await db.Productos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Nombre, p.Activo, p.CategoriaId, Categoria = p.Categoria.Nombre })
            .ToListAsync(ct);

        if (productos.Count != ids.Count)
            throw new ReglaNegocioException("Alguno de los productos no existe.", StatusCodes.Status404NotFound);

        var inactivo = productos.FirstOrDefault(p => !p.Activo);
        if (inactivo is not null)
            throw new ReglaNegocioException($"\"{inactivo.Nombre}\" está dado de baja y no puede formar parte de un combo.");
    }

    private async Task<ComboDto?> DtoAsync(int id, CancellationToken ct)
    {
        var combo = await db.Combos.AsNoTracking().Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id, ct);
        return combo is null ? null : (await ADtosAsync([combo], ct))[0];
    }

    /// <summary>Calcula precios (con ofertas vigentes), ahorro y stock disponible de cada combo.</summary>
    private async Task<List<ComboDto>> ADtosAsync(IReadOnlyList<Combo> combos, CancellationToken ct)
    {
        var ids = combos.SelectMany(c => c.Items.Select(i => i.ProductoId)).Distinct().ToList();
        var ahora = DateTime.UtcNow;

        var productos = await db.Productos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Nombre, p.ImagenUrl, p.Precio, p.Stock, p.Activo, Categoria = p.Categoria.Nombre })
            .ToDictionaryAsync(p => p.Id, ct);

        var ofertas = await db.Ofertas.AsNoTracking()
            .Where(o => ids.Contains(o.ProductoId) && o.Activa && o.Desde <= ahora && o.Hasta >= ahora)
            .GroupBy(o => o.ProductoId)
            .Select(g => new { ProductoId = g.Key, Pct = g.Max(o => o.Porcentaje) })
            .ToDictionaryAsync(o => o.ProductoId, o => o.Pct, ct);

        var lista = new List<ComboDto>();
        foreach (var combo in combos)
        {
            var items = new List<ComboItemDto>();
            foreach (var it in combo.Items.OrderBy(i => i.Id))
            {
                var p = productos[it.ProductoId];
                var precioCombo = Precios.ConDescuento(p.Precio, combo.Porcentaje);
                // No se acumulan descuentos: se aplica el mejor precio entre el combo y la oferta del producto.
                if (ofertas.TryGetValue(it.ProductoId, out var pct))
                    precioCombo = Math.Min(precioCombo, Precios.ConDescuento(p.Precio, pct));
                items.Add(new ComboItemDto(p.Id, p.Nombre, p.ImagenUrl, p.Categoria, it.Cantidad, p.Precio, precioCombo, p.Stock));
            }

            var precioLista = items.Sum(i => i.PrecioLista * i.Cantidad);
            var precioFinal = items.Sum(i => i.PrecioCombo * i.Cantidad);
            var stock = items.Count == 0 ? 0 : items.Min(i => i.Stock / i.Cantidad);
            var todosActivos = combo.Items.All(i => productos[i.ProductoId].Activo);

            lista.Add(new ComboDto(
                combo.Id, combo.Nombre, combo.Descripcion, combo.Porcentaje, combo.Activo,
                items.FirstOrDefault(i => i.ImagenUrl is not null)?.ImagenUrl,
                precioLista, precioFinal, precioLista - precioFinal, stock,
                combo.Activo && todosActivos && stock > 0, items));
        }
        return lista;
    }
}
