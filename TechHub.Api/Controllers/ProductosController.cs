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
        var query = db.Productos.AsNoTracking().AsQueryable();

        var esAdmin = User.IsInRole(Roles.Admin);
        if (!(q.IncluirInactivos && esAdmin))
            query = query.Where(p => p.Activo);

        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(p => p.Nombre.Contains(b) || p.Marca.Contains(b));
        }

        if (q.CategoriaId is int categoriaId)
            query = query.Where(p => p.CategoriaId == categoriaId);

        if (q.SoloStockBajo == true)
            query = query.Where(p => p.Stock <= p.StockMinimo);

        query = q.Orden?.ToLowerInvariant() switch
        {
            "precio" => query.OrderBy(p => p.Precio).ThenBy(p => p.Id),
            "precio_desc" => query.OrderByDescending(p => p.Precio).ThenBy(p => p.Id),
            "stock" => query.OrderBy(p => p.Stock).ThenBy(p => p.Id),
            _ => query.OrderBy(p => p.Nombre).ThenBy(p => p.Id)
        };

        return await query
            .Select(p => new ProductoDto(
                p.Id, p.Nombre, p.Marca, p.Descripcion, p.Precio, p.Stock, p.StockMinimo,
                p.Stock <= p.StockMinimo, p.ImagenUrl, p.Activo, p.CategoriaId, p.Categoria.Nombre))
            .ToPagedResultAsync(q.Pagina, q.Tamanio, ct);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductoDto>> Obtener(int id, CancellationToken ct)
    {
        var dto = await ProyectarAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Crea un producto con stock 0. El stock solo aumenta registrando Ingresos (trazabilidad).</summary>
    [HttpPost]
    [ProducesResponseType<ProductoDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductoDto>> Crear(ProductoRequest req, CancellationToken ct)
    {
        if (!await db.Categorias.AnyAsync(c => c.Id == req.CategoriaId, ct))
            return BadRequest(new ProblemDetails { Status = 400, Title = "Categoría inválida", Detail = "La categoría no existe." });

        var producto = new Producto
        {
            Nombre = req.Nombre.Trim(),
            Marca = req.Marca.Trim(),
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

        if (!await db.Categorias.AnyAsync(c => c.Id == req.CategoriaId, ct))
            return BadRequest(new ProblemDetails { Status = 400, Title = "Categoría inválida", Detail = "La categoría no existe." });

        producto.Nombre = req.Nombre.Trim();
        producto.Marca = req.Marca.Trim();
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

    private Task<ProductoDto?> ProyectarAsync(int id, CancellationToken ct) =>
        db.Productos.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProductoDto(
                p.Id, p.Nombre, p.Marca, p.Descripcion, p.Precio, p.Stock, p.StockMinimo,
                p.Stock <= p.StockMinimo, p.ImagenUrl, p.Activo, p.CategoriaId, p.Categoria.Nombre))
            .FirstOrDefaultAsync(ct);
}
