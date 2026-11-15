using TechHub.Api.DTOs;
using TechHub.Api.Models;

namespace TechHub.Api.Services;

/// <summary>Un producto junto con el descuento vigente y su precio final (se calcula en SQL).</summary>
public sealed class ProductoOferta
{
    public Producto P { get; set; } = null!;
    public int? Pct { get; set; }
    public decimal PrecioFinal { get; set; }
}

public static class ProductoConsultas
{
    /// <summary>
    /// Agrega a cada producto el mayor descuento de las ofertas activas y vigentes en este momento,
    /// y el precio final ya redondeado. Permite filtrar y ordenar por el precio final.
    /// </summary>
    public static IQueryable<ProductoOferta> ConOferta(this IQueryable<Producto> query, DateTime ahora) =>
        query.Select(p => new ProductoOferta
        {
            P = p,
            Pct = p.Ofertas.Where(o => o.Activa && o.Desde <= ahora && o.Hasta >= ahora).Max(o => (int?)o.Porcentaje),
            PrecioFinal = Math.Round(
                p.Precio * (100 - (p.Ofertas.Where(o => o.Activa && o.Desde <= ahora && o.Hasta >= ahora).Max(o => (int?)o.Porcentaje) ?? 0)) / 100m, 2)
        });

    public static IQueryable<ProductoDto> ADto(this IQueryable<ProductoOferta> query) =>
        query.Select(x => new ProductoDto(
            x.P.Id, x.P.Nombre, x.P.MarcaId, x.P.Marca.Nombre, x.P.Descripcion, x.P.Precio, x.P.Stock, x.P.StockMinimo,
            x.P.Stock <= x.P.StockMinimo, x.P.ImagenUrl, x.P.Activo, x.P.CategoriaId, x.P.Categoria.Nombre,
            x.Pct, x.PrecioFinal));
}
