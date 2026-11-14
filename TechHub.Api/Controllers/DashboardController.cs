using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;

namespace TechHub.Api.Controllers;

/// <summary>Indicadores para la pantalla de inicio del administrador.</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet("resumen")]
    public async Task<ActionResult<ResumenDto>> Resumen(CancellationToken ct)
    {
        // Día y mes en hora de Argentina (UTC-3); las fechas se guardan en UTC.
        var ahoraAr = DateTime.UtcNow.AddHours(-3);
        var hoyUtc = ahoraAr.Date.AddHours(3);
        var mesUtc = new DateTime(ahoraAr.Year, ahoraAr.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(3);

        var activos = db.Productos.AsNoTracking().Where(p => p.Activo);
        // Facturación = renglones de pedidos con pago confirmado o entregados.
        var cobrados = db.Pedidos.AsNoTracking().Where(p => EstadosPedido.CobradosIds.Contains(p.EstadoId));
        var renglones = db.DetallesPedido.AsNoTracking().Where(d => EstadosPedido.CobradosIds.Contains(d.Pedido.EstadoId));

        var resumen = new ResumenDto(
            TotalProductos: await activos.CountAsync(ct),
            ProductosStockBajo: await activos.CountAsync(p => p.Stock <= p.StockMinimo, ct),
            UnidadesEnStock: await activos.SumAsync(p => p.Stock, ct),
            ValorInventario: await activos.SumAsync(p => p.Precio * p.Stock, ct),
            PedidosPendientes: await db.Pedidos.CountAsync(p => p.EstadoId == EstadosPedido.PendienteId, ct),
            PagosEnRevision: await db.Pedidos.CountAsync(p => p.EstadoId == EstadosPedido.EnRevisionId, ct),
            VentasHoy: await cobrados.CountAsync(p => p.Fecha >= hoyUtc, ct),
            FacturacionHoy: await renglones.Where(d => d.Pedido.Fecha >= hoyUtc).SumAsync(d => d.Cantidad * d.PrecioUnitario, ct),
            FacturacionMes: await renglones.Where(d => d.Pedido.Fecha >= mesUtc).SumAsync(d => d.Cantidad * d.PrecioUnitario, ct),
            Clientes: await db.Clientes.CountAsync(c => c.Activo, ct),
            Proveedores: await db.Proveedores.CountAsync(p => p.Activo, ct));

        return Ok(resumen);
    }

    /// <summary>Facturación y cantidad de pedidos cobrados por día (hora de Argentina).</summary>
    [HttpGet("ventas-diarias")]
    public async Task<ActionResult<List<VentaDiaDto>>> VentasDiarias([FromQuery] int dias = 7, CancellationToken ct = default)
    {
        dias = Math.Clamp(dias, 1, 31);
        var hoyAr = DateTime.UtcNow.AddHours(-3).Date;
        var desdeAr = hoyAr.AddDays(-(dias - 1));
        var desdeUtc = desdeAr.AddHours(3);

        var filas = await db.Pedidos.AsNoTracking()
            .Where(p => EstadosPedido.CobradosIds.Contains(p.EstadoId) && p.Fecha >= desdeUtc)
            .Select(p => new { p.Fecha, Total = p.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario) })
            .ToListAsync(ct);

        var porDia = filas
            .GroupBy(f => f.Fecha.AddHours(-3).Date)
            .ToDictionary(g => g.Key, g => (Total: g.Sum(x => x.Total), Pedidos: g.Count()));

        var cultura = new System.Globalization.CultureInfo("es-AR");
        var resultado = Enumerable.Range(0, dias)
            .Select(i => desdeAr.AddDays(i))
            .Select(d => porDia.TryGetValue(d, out var v)
                ? new VentaDiaDto(d, d.ToString("ddd dd", cultura), v.Total, v.Pedidos)
                : new VentaDiaDto(d, d.ToString("ddd dd", cultura), 0, 0))
            .ToList();

        return Ok(resultado);
    }

    /// <summary>Ranking de productos más vendidos (unidades) en pedidos cobrados.</summary>
    [HttpGet("top-productos")]
    public async Task<ActionResult<List<TopProductoDto>>> TopProductos([FromQuery] int cantidad = 5, CancellationToken ct = default)
    {
        cantidad = Math.Clamp(cantidad, 1, 20);

        var ranking = await db.DetallesPedido.AsNoTracking()
            .Where(d => EstadosPedido.CobradosIds.Contains(d.Pedido.EstadoId))
            .GroupBy(d => d.ProductoId)
            .Select(g => new { ProductoId = g.Key, Unidades = g.Sum(d => d.Cantidad), Total = g.Sum(d => d.Cantidad * d.PrecioUnitario) })
            .OrderByDescending(x => x.Unidades)
            .Take(cantidad)
            .ToListAsync(ct);

        var ids = ranking.Select(r => r.ProductoId).ToList();
        var productos = await db.Productos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Nombre, p.ImagenUrl })
            .ToDictionaryAsync(p => p.Id, ct);

        return Ok(ranking
            .Where(r => productos.ContainsKey(r.ProductoId))
            .Select(r => new TopProductoDto(r.ProductoId, productos[r.ProductoId].Nombre, productos[r.ProductoId].ImagenUrl, r.Unidades, r.Total))
            .ToList());
    }

    /// <summary>Ventas cobradas agrupadas por marca (unidades y facturación), de mayor a menor.</summary>
    [HttpGet("ventas-por-marca")]
    public async Task<ActionResult<List<VentaMarcaDto>>> VentasPorMarca([FromQuery] int cantidad = 6, CancellationToken ct = default)
    {
        cantidad = Math.Clamp(cantidad, 1, 20);

        var lista = await db.DetallesPedido.AsNoTracking()
            .Where(d => EstadosPedido.CobradosIds.Contains(d.Pedido.EstadoId))
            .GroupBy(d => new { d.Producto.MarcaId, d.Producto.Marca.Nombre })
            .Select(g => new { g.Key.MarcaId, g.Key.Nombre, Unidades = g.Sum(d => d.Cantidad), Total = g.Sum(d => d.Cantidad * d.PrecioUnitario) })
            .OrderByDescending(x => x.Total)
            .Take(cantidad)
            .ToListAsync(ct);

        return Ok(lista.Select(x => new VentaMarcaDto(x.MarcaId, x.Nombre, x.Unidades, x.Total)).ToList());
    }
}
