namespace TechHub.Api.Services;

/// <summary>Datos de la cuenta donde los compradores transfieren (sección "Pagos" de appsettings).</summary>
public class PagosOptions
{
    public string Alias { get; set; } = string.Empty;
    public string Cbu { get; set; } = string.Empty;
    public string Titular { get; set; } = string.Empty;
    public string Cuit { get; set; } = string.Empty;
    public string Banco { get; set; } = string.Empty;
    /// <summary>Horas que se reserva el stock esperando el pago (informativo para el comprador).</summary>
    public int HorasReserva { get; set; } = 48;
}

/// <summary>
/// Configuración SMTP (sección "Email"). Para Gmail: smtp.gmail.com, puerto 587 y una
/// "contraseña de aplicación" de 16 caracteres (requiere verificación en 2 pasos en la cuenta).
/// La contraseña va en appsettings.Local.json / appsettings.Production.json, nunca en GitHub.
/// </summary>
public class EmailOptions
{
    public bool Habilitado { get; set; }
    public string Host { get; set; } = "smtp.gmail.com";
    public int Puerto { get; set; } = 587;
    public string Usuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string NombreRemitente { get; set; } = "TechHub";

    public bool EstaConfigurado => Habilitado && Usuario.Length > 0 && Password.Length > 0;
}
