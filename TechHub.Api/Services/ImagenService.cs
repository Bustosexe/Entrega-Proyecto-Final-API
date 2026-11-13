namespace TechHub.Api.Services;

public interface IImagenService
{
    /// <summary>Guarda la imagen en wwwroot/uploads con nombre GUID y devuelve la ruta relativa.</summary>
    Task<string> GuardarAsync(IFormFile archivo, CancellationToken ct = default);
    void Eliminar(string? rutaRelativa);
}

public class ImagenInvalidaException(string mensaje) : Exception(mensaje);

public class ImagenService(IWebHostEnvironment env, IConfiguration config) : IImagenService
{
    private const string Carpeta = "uploads";

    private static readonly Dictionary<string, string[]> TiposPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".png"] = ["image/png"],
        [".webp"] = ["image/webp"]
    };

    private string RutaCarpeta
    {
        get
        {
            var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
            return Path.Combine(webRoot, Carpeta);
        }
    }

    public async Task<string> GuardarAsync(IFormFile archivo, CancellationToken ct = default)
    {
        var maxBytes = config.GetValue<long?>("Uploads:MaxBytes") ?? 5 * 1024 * 1024;

        if (archivo is null || archivo.Length == 0)
            throw new ImagenInvalidaException("No se recibió ningún archivo.");

        if (archivo.Length > maxBytes)
            throw new ImagenInvalidaException($"La imagen supera el máximo de {maxBytes / 1024 / 1024} MB.");

        var extension = Path.GetExtension(archivo.FileName);
        if (string.IsNullOrEmpty(extension) || !TiposPermitidos.TryGetValue(extension, out var mimes)
            || !mimes.Contains(archivo.ContentType, StringComparer.OrdinalIgnoreCase))
            throw new ImagenInvalidaException("Formato no permitido. Solo JPG, PNG o WEBP.");

        if (!await TieneFirmaDeImagenAsync(archivo, ct))
            throw new ImagenInvalidaException("El archivo no es una imagen válida.");

        Directory.CreateDirectory(RutaCarpeta);

        // Nombre único: evita colisiones y que el usuario controle el nombre del archivo en el servidor.
        var nombre = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var rutaFisica = Path.Combine(RutaCarpeta, nombre);

        await using (var stream = new FileStream(rutaFisica, FileMode.CreateNew))
        {
            await archivo.CopyToAsync(stream, ct);
        }

        return $"/{Carpeta}/{nombre}";
    }

    public void Eliminar(string? rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa) || !rutaRelativa.StartsWith($"/{Carpeta}/"))
            return;

        var nombre = Path.GetFileName(rutaRelativa);
        var rutaFisica = Path.Combine(RutaCarpeta, nombre);
        if (File.Exists(rutaFisica))
            File.Delete(rutaFisica);
    }

    /// <summary>Verifica los primeros bytes (magic numbers) para no confiar solo en la extensión.</summary>
    private static async Task<bool> TieneFirmaDeImagenAsync(IFormFile archivo, CancellationToken ct)
    {
        var cabecera = new byte[12];
        await using var s = archivo.OpenReadStream();
        var leidos = await s.ReadAtLeastAsync(cabecera, cabecera.Length, throwOnEndOfStream: false, ct);
        if (leidos < 4) return false;

        var esJpg = cabecera[0] == 0xFF && cabecera[1] == 0xD8 && cabecera[2] == 0xFF;
        var esPng = cabecera[0] == 0x89 && cabecera[1] == 0x50 && cabecera[2] == 0x4E && cabecera[3] == 0x47;
        var esWebp = leidos >= 12
            && cabecera[0] == 0x52 && cabecera[1] == 0x49 && cabecera[2] == 0x46 && cabecera[3] == 0x46   // RIFF
            && cabecera[8] == 0x57 && cabecera[9] == 0x45 && cabecera[10] == 0x42 && cabecera[11] == 0x50; // WEBP
        return esJpg || esPng || esWebp;
    }
}
