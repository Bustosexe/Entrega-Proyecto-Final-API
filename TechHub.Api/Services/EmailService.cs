using System.Threading.Channels;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TechHub.Api.Services;

public record AdjuntoEmail(string NombreArchivo, byte[] Contenido, string Mime = "application/pdf");

public record EmailMensaje(string Para, string Asunto, string Html, IReadOnlyList<AdjuntoEmail>? Adjuntos = null);

public interface IEmailService
{
    bool EstaConfigurado { get; }
    /// <summary>Envía en el momento (para el envío manual de reportes: el usuario ve el error si falla).</summary>
    Task EnviarAsync(EmailMensaje mensaje, CancellationToken ct = default);
    /// <summary>Encola el envío: la respuesta HTTP no espera al servidor de correo (emails automáticos).</summary>
    void Encolar(EmailMensaje mensaje);
}

/// <summary>
/// Envío de emails por SMTP con MailKit. Con Gmail se usa smtp.gmail.com:587 (STARTTLS)
/// y la contraseña de aplicación de 16 caracteres de la cuenta.
/// </summary>
public class EmailService(IOptions<EmailOptions> opciones, Channel<EmailMensaje> cola, ILogger<EmailService> logger) : IEmailService
{
    private readonly EmailOptions _opt = opciones.Value;

    public bool EstaConfigurado => _opt.EstaConfigurado;

    public async Task EnviarAsync(EmailMensaje mensaje, CancellationToken ct = default)
    {
        if (!EstaConfigurado)
            throw new ReglaNegocioException("El envío de emails no está configurado en el servidor (sección Email de appsettings).");

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_opt.NombreRemitente, _opt.Usuario));
        mime.To.Add(MailboxAddress.Parse(mensaje.Para));
        mime.Subject = mensaje.Asunto;

        var cuerpo = new BodyBuilder { HtmlBody = mensaje.Html };
        foreach (var a in mensaje.Adjuntos ?? [])
            cuerpo.Attachments.Add(a.NombreArchivo, a.Contenido, ContentType.Parse(a.Mime));
        mime.Body = cuerpo.ToMessageBody();

        using var smtp = new SmtpClient();
        try
        {
            await smtp.ConnectAsync(_opt.Host, _opt.Puerto, SecureSocketOptions.StartTls, ct);
            await smtp.AuthenticateAsync(_opt.Usuario, _opt.Password.Replace(" ", ""), ct);
            await smtp.SendAsync(mime, ct);
            await smtp.DisconnectAsync(true, ct);
            logger.LogInformation("Email enviado a {Para}: {Asunto}", mensaje.Para, mensaje.Asunto);
        }
        catch (AuthenticationException ex)
        {
            logger.LogError(ex, "Gmail rechazó el usuario o la contraseña de aplicación.");
            throw new ReglaNegocioException("El servidor de correo rechazó las credenciales. Revisá el usuario y la contraseña de aplicación de Gmail.",
                StatusCodes.Status502BadGateway);
        }
        catch (Exception ex) when (ex is SmtpCommandException or SmtpProtocolException or IOException or System.Net.Sockets.SocketException)
        {
            logger.LogError(ex, "No se pudo enviar el email a {Para}", mensaje.Para);
            throw new ReglaNegocioException("No se pudo conectar con el servidor de correo. Intentá más tarde.", StatusCodes.Status502BadGateway);
        }
    }

    public void Encolar(EmailMensaje mensaje)
    {
        if (!EstaConfigurado)
        {
            logger.LogInformation("Email no enviado (envío deshabilitado): {Asunto} → {Para}", mensaje.Asunto, mensaje.Para);
            return;
        }
        if (!cola.Writer.TryWrite(mensaje))
            logger.LogWarning("La cola de emails está llena; se descartó: {Asunto}", mensaje.Asunto);
    }
}

/// <summary>Servicio en segundo plano que envía los emails encolados (con un reintento).</summary>
public class EmailBackgroundService(Channel<EmailMensaje> cola, IServiceProvider services, ILogger<EmailBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var mensaje in cola.Reader.ReadAllAsync(stoppingToken))
        {
            for (var intento = 1; intento <= 2; intento++)
            {
                try
                {
                    using var scope = services.CreateScope();
                    var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    await email.EnviarAsync(mensaje, stoppingToken);
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Falló el envío del email (intento {Intento}): {Asunto}", intento, mensaje.Asunto);
                    if (intento < 2) await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }
    }
}
