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
/// Reportes en PDF: comprobante de pedido (comprador dueño o admin), inventario valorizado y ventas por período (admin).
/// Todos se pueden descargar o enviar por email (Gmail con contraseña de aplicación).
/// </summary>
[ApiController]
[Route("api/reportes")]
[Authorize(Roles = Roles.Todos)]
public class ReportesController(
    AppDbContext db,
    IReportesPdfService pdf,
    IEmailService email,
    INotificacionesService notificaciones,
    ILogger<ReportesController> logger) : ControllerBase
{
    private const string Pdf = "application/pdf";

    /// <summary>Comprobante del pedido en PDF.</summary>
    [HttpGet("pedido/{codigo}")]
    [Produces(Pdf)]
    public async Task<IActionResult> Pedido(string codigo, CancellationToken ct)
    {
        var (bytes, _) = await GenerarPedidoAsync(codigo, ct);
        return File(bytes, Pdf, $"TechHub-Pedido-{codigo}.pdf");
    }

    /// <summary>Inventario valorizado: stock, precio y valor de cada producto activo.</summary>
    [HttpGet("inventario")]
    [Authorize(Roles = Roles.Admin)]
    [Produces(Pdf)]
    public async Task<IActionResult> Inventario(CancellationToken ct) =>
        File(await pdf.InventarioAsync(ct), Pdf, $"TechHub-Inventario-{HoyAr():yyyyMMdd}.pdf");

    /// <summary>Ventas cobradas entre dos fechas (inclusive). Por defecto, los últimos 30 días.</summary>
    [HttpGet("ventas")]
    [Authorize(Roles = Roles.Admin)]
    [Produces(Pdf)]
    public async Task<IActionResult> Ventas([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, CancellationToken ct)
    {
        var (d, h) = Rango(desde, hasta);
        return File(await pdf.VentasAsync(d, h, ct), Pdf, $"TechHub-Ventas-{d:yyyyMMdd}-{h:yyyyMMdd}.pdf");
    }

    /// <summary>
    /// Envía un reporte por email como adjunto. El comprador solo puede enviarse sus propios pedidos a su email.
    /// Requiere configurar la sección "Email" (Gmail: contraseña de aplicación de 16 caracteres).
    /// </summary>
    [HttpPost("enviar")]
    [Produces("application/json")]
    public async Task<IActionResult> Enviar(EnviarReporteRequest req, CancellationToken ct)
    {
        var esAdmin = User.IsInRole(Roles.Admin);
        if (!esAdmin && req.Tipo != "pedido") return Forbid();

        var usuarioId = User.ObtenerUsuarioId();
        var emailUsuario = await db.Usuarios.Where(u => u.Id == usuarioId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        // Un comprador no puede elegir el destinatario: siempre es su propio email.
        var destino = esAdmin && !string.IsNullOrWhiteSpace(req.Email) ? req.Email.Trim() : emailUsuario;
        if (string.IsNullOrWhiteSpace(destino))
            return BadRequest(new ProblemDetails { Status = 400, Title = "Email requerido", Detail = "Indicá el email de destino." });

        byte[] bytes;
        string archivo, titulo, detalle;
        switch (req.Tipo)
        {
            case "pedido":
                if (string.IsNullOrWhiteSpace(req.Codigo))
                    return BadRequest(new ProblemDetails { Status = 400, Title = "Pedido requerido", Detail = "Indicá el código del pedido." });
                (bytes, var pedido) = await GenerarPedidoAsync(req.Codigo, ct);
                archivo = $"TechHub-Pedido-{pedido.Codigo}.pdf";
                titulo = $"Pedido {pedido.Codigo}";
                detalle = $"Comprobante del pedido {pedido.Codigo} ({EstadosPedido.Texto(pedido.Estado)}).";
                break;
            case "inventario":
                bytes = await pdf.InventarioAsync(ct);
                archivo = $"TechHub-Inventario-{HoyAr():yyyyMMdd}.pdf";
                titulo = "Inventario valorizado";
                detalle = $"Inventario valorizado al {HoyAr():dd/MM/yyyy}.";
                break;
            default:
                var (d, h) = Rango(req.Desde, req.Hasta);
                bytes = await pdf.VentasAsync(d, h, ct);
                archivo = $"TechHub-Ventas-{d:yyyyMMdd}-{h:yyyyMMdd}.pdf";
                titulo = "Reporte de ventas";
                detalle = $"Ventas del {d:dd/MM/yyyy} al {h:dd/MM/yyyy}.";
                break;
        }

        await email.EnviarAsync(new EmailMensaje(destino, $"TechHub · {titulo}", notificaciones.HtmlReporte(titulo, detalle),
            [new AdjuntoEmail(archivo, bytes)]), ct);
        logger.LogInformation("Reporte {Tipo} enviado a {Destino}", req.Tipo, destino);

        return Ok(new { mensaje = $"Reporte enviado a {destino}." });
    }

    // ---------------------------------------------------------------------

    private async Task<(byte[] Bytes, PedidoDto Pedido)> GenerarPedidoAsync(string codigo, CancellationToken ct)
    {
        var pedido = await db.ObtenerPedidoAsync(codigo, ct)
            ?? throw new ReglaNegocioException("El pedido no existe.", StatusCodes.Status404NotFound);

        if (!User.IsInRole(Roles.Admin))
        {
            var usuarioId = User.ObtenerUsuarioId();
            var clienteId = await db.Usuarios.Where(u => u.Id == usuarioId).Select(u => u.ClienteId).FirstOrDefaultAsync(ct);
            if (clienteId != pedido.ClienteId)
                throw new ReglaNegocioException("No podés ver pedidos de otra cuenta.", StatusCodes.Status403Forbidden);
        }

        var cliente = await db.Clientes.AsNoTracking()
            .Where(c => c.Id == pedido.ClienteId)
            .Select(c => new { c.Email, c.Documento })
            .FirstAsync(ct);

        return (pdf.ComprobantePedido(pedido, cliente.Email, cliente.Documento), pedido);
    }

    private static DateTime HoyAr() => DateTime.UtcNow.AddHours(-3).Date;

    private static (DateTime Desde, DateTime Hasta) Rango(DateTime? desde, DateTime? hasta)
    {
        var h = (hasta ?? HoyAr()).Date;
        var d = (desde ?? h.AddDays(-29)).Date;
        if (d > h) (d, h) = (h, d);
        if ((h - d).TotalDays > 366) d = h.AddDays(-366);
        return (d, h);
    }
}
