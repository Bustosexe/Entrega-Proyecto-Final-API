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

    // URL del sitio en MonsterASP.
    // En el plan gratuito HTTPS se activa pidiéndolo a soporte; hasta entonces se usa http://
    // (permitido solo para este dominio en Platforms/Android/Resources/xml/network_security_config.xml).
    public const string UrlProduccion = "http://apiintegrador.runasp.net";

    public static string BaseUrl => UsarServidorLocal ? UrlLocal : UrlProduccion;

    /// <summary>
    /// Versión de las fotos de demo (wwwroot/demo). Al reemplazar esas fotos se sube este número
    /// para que la app no siga mostrando las anteriores guardadas en caché.
    /// (Las fotos que sube el admin no lo necesitan: cada una tiene un nombre único).
    /// </summary>
    public const int VersionFotosDemo = 2;

    /// <summary>Convierte la ruta relativa de una imagen (/uploads/x.jpg) en URL completa.</summary>
    public static string UrlCompleta(string? rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa)) return "producto_placeholder.png";
        if (rutaRelativa.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return rutaRelativa;

        var url = BaseUrl.TrimEnd('/') + rutaRelativa;
        return rutaRelativa.StartsWith("/demo/", StringComparison.OrdinalIgnoreCase)
            ? $"{url}?v={VersionFotosDemo}"
            : url;
    }
}
