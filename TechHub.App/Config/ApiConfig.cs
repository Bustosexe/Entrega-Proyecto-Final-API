namespace TechHub.App.Config;

/// <summary>
/// Dirección de la API.
///   - Emulador de Android contra la API corriendo en tu PC: http://10.0.2.2:5180
///     (10.0.2.2 es la PC vista desde el emulador; ejecutá la API con el perfil "http").
///   - Teléfono real / APK final: la URL de MonsterASP (https).
/// </summary>
public static class ApiConfig
{
    /// <summary>true = API local (emulador). false = API publicada en MonsterASP.</summary>
    public const bool UsarServidorLocal = true;

    public const string UrlLocal = "http://10.0.2.2:5180";

    // TODO: reemplazar por la URL que te da el panel de MonsterASP.
    public const string UrlProduccion = "https://techhub.runasp.net";

    public static string BaseUrl => UsarServidorLocal ? UrlLocal : UrlProduccion;

    /// <summary>Convierte la ruta relativa de una imagen (/uploads/x.jpg) en URL completa.</summary>
    public static string UrlCompleta(string? rutaRelativa) =>
        string.IsNullOrWhiteSpace(rutaRelativa)
            ? "producto_placeholder.png"
            : rutaRelativa.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? rutaRelativa
                : BaseUrl.TrimEnd('/') + rutaRelativa;
}
