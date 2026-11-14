using System.Globalization;
using System.Net;
using Microsoft.Extensions.Options;
using TechHub.Api.DTOs;

namespace TechHub.Api.Services;

public interface INotificacionesService
{
    /// <summary>Email al comprador con el resumen del pedido y los datos para transferir.</summary>
    void PedidoCreado(PedidoDto pedido, string? emailCliente);
    /// <summary>Email al comprador cuando el admin confirma el pago, con el comprobante en PDF.</summary>
    void PagoConfirmado(PedidoDto pedido, string? emailCliente, string? documentoCliente);
    /// <summary>Plantilla HTML simple para envíos manuales de reportes.</summary>
    string HtmlReporte(string titulo, string detalle);
}

/// <summary>Arma los emails automáticos (HTML minimalista) y los deja en la cola de envío.</summary>
public class NotificacionesService(IEmailService email, IReportesPdfService pdf, IOptions<PagosOptions> pagos,
    ILogger<NotificacionesService> logger) : INotificacionesService
{
    private static readonly CultureInfo Ar = new("es-AR");
    private static string Moneda(decimal v) => v.ToString("C", Ar);
    private static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    public void PedidoCreado(PedidoDto pedido, string? emailCliente)
    {
        if (string.IsNullOrWhiteSpace(emailCliente)) return;
        var p = pagos.Value;
        var cuerpo = $"""
            <p>¡Hola {H(pedido.Cliente)}! Recibimos tu pedido <b>{H(pedido.Codigo)}</b>.</p>
            {TablaItems(pedido)}
            <p style="margin:24px 0 8px;font-weight:600">Para completar la compra, transferí {Moneda(pedido.Total)} a:</p>
            <table style="width:100%;background:#F9FAFB;border:1px solid #E5E7EB;border-radius:12px;padding:12px;font-size:14px">
              <tr><td style="color:#6B7280;padding:4px 0">Alias</td><td style="font-weight:600">{H(p.Alias)}</td></tr>
              <tr><td style="color:#6B7280;padding:4px 0">CBU/CVU</td><td>{H(p.Cbu)}</td></tr>
              <tr><td style="color:#6B7280;padding:4px 0">Titular</td><td>{H(p.Titular)} · CUIT {H(p.Cuit)}</td></tr>
              <tr><td style="color:#6B7280;padding:4px 0">Banco</td><td>{H(p.Banco)}</td></tr>
            </table>
            <p>Después subí el comprobante desde la app, en <b>Mis pedidos</b>. Reservamos el stock por {p.HorasReserva} horas.</p>
            """;
        email.Encolar(new EmailMensaje(emailCliente, $"Recibimos tu pedido {pedido.Codigo}", Plantilla("Pedido recibido", cuerpo)));
    }

    public void PagoConfirmado(PedidoDto pedido, string? emailCliente, string? documentoCliente)
    {
        if (string.IsNullOrWhiteSpace(emailCliente)) return;
        try
        {
            var adjunto = pdf.ComprobantePedido(pedido, emailCliente, documentoCliente);
            var cuerpo = $"""
                <p>¡Hola {H(pedido.Cliente)}! Confirmamos el pago de tu pedido <b>{H(pedido.Codigo)}</b>.</p>
                {TablaItems(pedido)}
                <p>Te adjuntamos el comprobante en PDF. Te avisaremos cuando esté listo para retirar o enviar.</p>
                """;
            email.Encolar(new EmailMensaje(emailCliente, $"Pago confirmado · Pedido {pedido.Codigo}",
                Plantilla("Pago confirmado", cuerpo), [new AdjuntoEmail($"TechHub-{pedido.Codigo}.pdf", adjunto)]));
        }
        catch (Exception ex)
        {
            // Un error al armar el email nunca debe impedir confirmar el pedido.
            logger.LogError(ex, "No se pudo preparar el email de pago confirmado del pedido {Codigo}", pedido.Codigo);
        }
    }

    public string HtmlReporte(string titulo, string detalle) =>
        Plantilla(titulo, $"<p>{H(detalle)}</p><p>Encontrás el reporte adjunto en PDF.</p>");

    private static string TablaItems(PedidoDto p)
    {
        var filas = string.Concat(p.Items.Select(i => $"""
            <tr>
              <td style="padding:8px 0;border-bottom:1px solid #F3F4F6">{H(i.Producto)}<br><span style="color:#6B7280;font-size:12px">{i.Cantidad} x {Moneda(i.PrecioUnitario)}</span></td>
              <td style="padding:8px 0;border-bottom:1px solid #F3F4F6;text-align:right;white-space:nowrap">{Moneda(i.Subtotal)}</td>
            </tr>
            """));
        return $"""
            <table style="width:100%;border-collapse:collapse;font-size:14px;margin-top:16px">
              {filas}
              <tr><td style="padding-top:12px;font-weight:600">Total</td><td style="padding-top:12px;text-align:right;font-weight:700;font-size:18px">{Moneda(p.Total)}</td></tr>
            </table>
            """;
    }

    private static string Plantilla(string titulo, string cuerpo) => $"""
        <!doctype html>
        <html><body style="margin:0;background:#F9FAFB;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#111827">
          <div style="max-width:560px;margin:0 auto;padding:32px 20px">
            <div style="font-size:22px;font-weight:800;letter-spacing:-0.5px">TechHub</div>
            <div style="background:#FFFFFF;border:1px solid #E5E7EB;border-radius:16px;padding:28px;margin-top:16px">
              <h1 style="font-size:20px;margin:0 0 12px">{H(titulo)}</h1>
              <div style="font-size:15px;line-height:1.55">{cuerpo}</div>
            </div>
            <p style="color:#9CA3AF;font-size:12px;text-align:center;margin-top:20px">TechHub · Hardware y componentes de PC</p>
          </div>
        </body></html>
        """;
}
