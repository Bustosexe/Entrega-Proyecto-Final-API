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
        // Facturación = pedidos con pago confirmado o entregados.
        var ventas = db.Salidas.AsNoTracking().Where(s => EstadosPedido.Cobrados.Contains(s.Estado));

        var resumen = new ResumenDto(
            TotalProductos: await activos.CountAsync(ct),
            ProductosStockBajo: await activos.CountAsync(p => p.Stock <= p.StockMinimo, ct),
            UnidadesEnStock: await activos.SumAsync(p => p.Stock, ct),
            ValorInventario: await activos.SumAsync(p => p.Precio * p.Stock, ct),
            PedidosPendientes: await db.Salidas.Where(s => s.Estado == EstadosPedido.Pendiente)
                .Select(s => s.CodigoPedido).Distinct().CountAsync(ct),
            PagosEnRevision: await db.Salidas.Where(s => s.Estado == EstadosPedido.EnRevision)
                .Select(s => s.CodigoPedido).Distinct().CountAsync(ct),
            VentasHoy: await ventas.Where(s => s.Fecha >= hoyUtc).Select(s => s.CodigoPedido).Distinct().CountAsync(ct),
            FacturacionHoy: await ventas.Where(s => s.Fecha >= hoyUtc).SumAsync(s => s.Total, ct),
            FacturacionMes: await ventas.Where(s => s.Fecha >= mesUtc).SumAsync(s => s.Total, ct),
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

        var filas = await db.Salidas.AsNoTracking()
            .Where(s => EstadosPedido.Cobrados.Contains(s.Estado) && s.Fecha >= desdeUtc)
            .Select(s => new { s.Fecha, s.Total, s.CodigoPedido })
            .ToListAsync(ct);

        var porDia = filas
            .GroupBy(f => f.Fecha.AddHours(-3).Date)
            .ToDictionary(g => g.Key, g => (Total: g.Sum(x => x.Total), Pedidos: g.Select(x => x.CodigoPedido).Distinct().Count()));

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

        var ranking = await db.Salidas.AsNoTracking()
            .Where(s => EstadosPedido.Cobrados.Contains(s.Estado))
            .GroupBy(s => s.ProductoId)
            .Select(g => new { ProductoId = g.Key, Unidades = g.Sum(s => s.Cantidad), Total = g.Sum(s => s.Total) })
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
}
