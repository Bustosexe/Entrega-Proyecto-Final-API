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
        var ventas = db.Salidas.AsNoTracking().Where(s => s.Estado != EstadosPedido.Cancelado);

        var resumen = new ResumenDto(
            TotalProductos: await activos.CountAsync(ct),
            ProductosStockBajo: await activos.CountAsync(p => p.Stock <= p.StockMinimo, ct),
            UnidadesEnStock: await activos.SumAsync(p => p.Stock, ct),
            ValorInventario: await activos.SumAsync(p => p.Precio * p.Stock, ct),
            PedidosPendientes: await db.Salidas.Where(s => s.Estado == EstadosPedido.Pendiente)
                .Select(s => s.CodigoPedido).Distinct().CountAsync(ct),
            VentasHoy: await ventas.Where(s => s.Fecha >= hoyUtc).Select(s => s.CodigoPedido).Distinct().CountAsync(ct),
            FacturacionHoy: await ventas.Where(s => s.Fecha >= hoyUtc).SumAsync(s => s.Total, ct),
            FacturacionMes: await ventas.Where(s => s.Fecha >= mesUtc).SumAsync(s => s.Total, ct),
            Clientes: await db.Clientes.CountAsync(c => c.Activo, ct),
            Proveedores: await db.Proveedores.CountAsync(p => p.Activo, ct));

        return Ok(resumen);
    }
}
