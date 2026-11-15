using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Common;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;
using TechHub.Api.Services;

namespace TechHub.Api.Controllers;

[ApiController]
[Route("api/productos")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class ProductosController(AppDbContext db, IImagenService imagenes) : ControllerBase
{
    /// <summary>
    /// Catálogo paginado con búsqueda, filtro por categoría y orden.
    /// Público (visitantes y compradores) para productos activos; el admin puede pedir también los inactivos.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ProductoDto>>> Listar([FromQuery] ProductoQuery q, CancellationToken ct)
    {
        // Cada producto trae su descuento vigente: el precio final sirve para filtrar y ordenar.
        var query = db.Productos.AsNoTracking().ConOferta(DateTime.UtcNow);

        var esAdmin = User.IsInRole(Roles.Admin);
        if (!(q.IncluirInactivos && esAdmin))
            query = query.Where(x => x.P.Activo);

        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(x => x.P.Nombre.Contains(b) || x.P.Marca.Nombre.Contains(b));
        }

        if (q.CategoriaId is int categoriaId)
            query = query.Where(x => x.P.CategoriaId == categoriaId);

        if (q.MarcaId is int marcaId)
            query = query.Where(x => x.P.MarcaId == marcaId);

        if (q.SoloStockBajo == true)
            query = query.Where(x => x.P.Stock <= x.P.StockMinimo);

        if (q.SoloOfertas == true)
            query = query.Where(x => x.Pct != null);

        if (q.PrecioMin is decimal min) query = query.Where(x => x.PrecioFinal >= min);
        if (q.PrecioMax is decimal max) query = query.Where(x => x.PrecioFinal <= max);

        var ordenados = q.Orden?.ToLowerInvariant() switch
        {
            "precio" => query.OrderBy(x => x.PrecioFinal).ThenBy(x => x.P.Id),
            "precio_desc" => query.OrderByDescending(x => x.PrecioFinal).ThenBy(x => x.P.Id),
            "stock" => query.OrderBy(x => x.P.Stock).ThenBy(x => x.P.Id),
            "nuevos" => query.OrderByDescending(x => x.P.FechaCreacion).ThenByDescending(x => x.P.Id),
            // Más vendidos: unidades vendidas en pedidos no cancelados (subconsulta en SQL).
            "vendidos" => query
                .OrderByDescending(x => x.P.DetallesPedido.Where(d => d.Pedido.EstadoId != EstadosPedido.CanceladoId).Sum(d => (int?)d.Cantidad) ?? 0)
                .ThenBy(x => x.P.Nombre),
            _ => query.OrderBy(x => x.P.Nombre).ThenBy(x => x.P.Id)
        };

        return await ordenados.ADto().ToPagedResultAsync(q.Pagina, q.Tamanio, ct);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductoDto>> Obtener(int id, CancellationToken ct)
    {
        var dto = await ProyectarAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Productos relacionados (misma categoría, activos), priorizando los que tienen stock y los más vendidos.
    /// Se usa en el detalle del producto: "También te puede interesar".
    /// </summary>
    [HttpGet("{id:int}/relacionados")]
    [AllowAnonymous]
    public async Task<ActionResult<List<ProductoDto>>> Relacionados(int id, [FromQuery] int cantidad = 6, CancellationToken ct = default)
    {
        var categoriaId = await db.Productos.Where(p => p.Id == id).Select(p => (int?)p.CategoriaId).FirstOrDefaultAsync(ct);
        if (categoriaId is null) return NotFound();

        cantidad = Math.Clamp(cantidad, 1, 12);
        var lista = await db.Productos.AsNoTracking()
            .ConOferta(DateTime.UtcNow)
            .Where(x => x.P.Activo && x.P.CategoriaId == categoriaId && x.P.Id != id)
            .OrderByDescending(x => x.P.Stock > 0)
            .ThenByDescending(x => x.P.DetallesPedido.Where(d => d.Pedido.EstadoId != EstadosPedido.CanceladoId).Sum(d => (int?)d.Cantidad) ?? 0)
            .ThenBy(x => x.P.Nombre)
            .Take(cantidad)
            .ADto()
            .ToListAsync(ct);

        return Ok(lista);
    }

    /// <summary>Crea un producto con stock 0. El stock solo aumenta registrando Compras (trazabilidad).</summary>
    [HttpPost]
    [ProducesResponseType<ProductoDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductoDto>> Crear(ProductoRequest req, CancellationToken ct)
    {
        if (await ValidarReferenciasAsync(req, ct) is { } error) return error;

        var producto = new Producto
        {
            Nombre = req.Nombre.Trim(),
            MarcaId = req.MarcaId,
            Descripcion = req.Descripcion?.Trim(),
            Precio = req.Precio,
            StockMinimo = req.StockMinimo,
            CategoriaId = req.CategoriaId,
            Activo = req.Activo,
            Stock = 0
        };
        db.Productos.Add(producto);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Obtener), new { id = producto.Id }, await ProyectarAsync(producto.Id, ct));
    }

    /// <summary>Modifica datos y precio. El stock no se edita a mano.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductoDto>> Actualizar(int id, ProductoRequest req, CancellationToken ct)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (producto is null) return NotFound();

        if (await ValidarReferenciasAsync(req, ct) is { } error) return error;

        producto.Nombre = req.Nombre.Trim();
        producto.MarcaId = req.MarcaId;
        producto.Descripcion = req.Descripcion?.Trim();
        producto.Precio = req.Precio;
        producto.StockMinimo = req.StockMinimo;
        producto.CategoriaId = req.CategoriaId;
        producto.Activo = req.Activo;
        await db.SaveChangesAsync(ct);

        return Ok(await ProyectarAsync(id, ct));
    }

    /// <summary>Baja lógica: el producto deja de verse en el catálogo pero su historial de ventas se conserva.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (producto is null) return NotFound();

        producto.Activo = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Sube la foto del producto (JPG, PNG o WEBP, máx. 5 MB). Se guarda en wwwroot/uploads con un nombre GUID
    /// y la ruta queda registrada en la base de datos. Si ya tenía una imagen, la anterior se elimina.
    /// </summary>
    [HttpPost("{id:int}/imagen")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType<ProductoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductoDto>> SubirImagen(int id, IFormFile archivo, CancellationToken ct)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (producto is null) return NotFound();

        var anterior = producto.ImagenUrl;
        producto.ImagenUrl = await imagenes.GuardarAsync(archivo, ct);
        await db.SaveChangesAsync(ct);
        imagenes.Eliminar(anterior);

        return Ok(await ProyectarAsync(id, ct));
    }

    [HttpDelete("{id:int}/imagen")]
    public async Task<IActionResult> EliminarImagen(int id, CancellationToken ct)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (producto is null) return NotFound();

        var anterior = producto.ImagenUrl;
        producto.ImagenUrl = null;
        await db.SaveChangesAsync(ct);
        imagenes.Eliminar(anterior);
        return NoContent();
    }

    private async Task<ActionResult?> ValidarReferenciasAsync(ProductoRequest req, CancellationToken ct)
    {
        if (!await db.Categorias.AnyAsync(c => c.Id == req.CategoriaId, ct))
            return BadRequest(new ProblemDetails { Status = 400, Title = "Categoría inválida", Detail = "La categoría no existe." });
        if (!await db.Marcas.AnyAsync(m => m.Id == req.MarcaId, ct))
            return BadRequest(new ProblemDetails { Status = 400, Title = "Marca inválida", Detail = "La marca no existe." });
        return null;
    }

    private Task<ProductoDto?> ProyectarAsync(int id, CancellationToken ct) =>
        db.Productos.AsNoTracking()
            .Where(p => p.Id == id)
            .ConOferta(DateTime.UtcNow)
            .ADto()
            .FirstOrDefaultAsync(ct);
}
