namespace TechHub.Api.Services;

public interface IComprobanteService
{
    /// <summary>Guarda el comprobante en la carpeta privada y devuelve el nombre de archivo generado.</summary>
    Task<string> GuardarAsync(string codigoPedido, IFormFile archivo, CancellationToken ct = default);
    /// <summary>Ruta física y tipo MIME del comprobante, o null si no existe.</summary>
    (string Ruta, string Mime)? Obtener(string? nombreArchivo);
    void Eliminar(string? nombreArchivo);
}

/// <summary>
/// Comprobantes de transferencia. A diferencia de las fotos de productos, NO se guardan en wwwroot:
/// van a App_Data/comprobantes (IIS no sirve esa carpeta) y solo se entregan por un endpoint
/// que verifica que quien pide sea el dueño del pedido o un administrador.
/// </summary>
public class ComprobanteService(IWebHostEnvironment env, IConfiguration config) : IComprobanteService
{
    private static readonly Dictionary<string, string> Mimes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf"
    };

    private string Carpeta => Path.Combine(env.ContentRootPath, "App_Data", "comprobantes");

    public async Task<string> GuardarAsync(string codigoPedido, IFormFile archivo, CancellationToken ct = default)
    {
        var maxBytes = config.GetValue<long?>("Uploads:MaxBytes") ?? 5 * 1024 * 1024;

        if (archivo is null || archivo.Length == 0)
            throw new ImagenInvalidaException("No se recibió ningún archivo.");
        if (archivo.Length > maxBytes)
            throw new ImagenInvalidaException($"El comprobante supera el máximo de {maxBytes / 1024 / 1024} MB.");

        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        if (!Mimes.ContainsKey(extension))
            throw new ImagenInvalidaException("Formato no permitido. Subí una imagen (JPG, PNG, WEBP) o un PDF.");
        if (!await FirmaValidaAsync(archivo, extension, ct))
            throw new ImagenInvalidaException("El archivo no es una imagen o un PDF válido.");

        Directory.CreateDirectory(Carpeta);
        var seguro = new string(codigoPedido.Where(char.IsLetterOrDigit).ToArray());
        var nombre = $"{seguro}-{Guid.NewGuid():N}{extension}";

        await using var stream = new FileStream(Path.Combine(Carpeta, nombre), FileMode.CreateNew);
        await archivo.CopyToAsync(stream, ct);
        return nombre;
    }

    public (string Ruta, string Mime)? Obtener(string? nombreArchivo)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo)) return null;
        var ruta = Path.Combine(Carpeta, Path.GetFileName(nombreArchivo));
        if (!File.Exists(ruta)) return null;
        return (ruta, Mimes.GetValueOrDefault(Path.GetExtension(ruta), "application/octet-stream"));
    }

    public void Eliminar(string? nombreArchivo)
    {
        if (Obtener(nombreArchivo) is { } archivo)
            File.Delete(archivo.Ruta);
    }

    private static async Task<bool> FirmaValidaAsync(IFormFile archivo, string extension, CancellationToken ct)
    {
        var c = new byte[12];
        await using var s = archivo.OpenReadStream();
        var n = await s.ReadAtLeastAsync(c, c.Length, throwOnEndOfStream: false, ct);
        if (n < 4) return false;

        return extension switch
        {
            ".pdf" => c[0] == 0x25 && c[1] == 0x50 && c[2] == 0x44 && c[3] == 0x46, // %PDF
            ".png" => c[0] == 0x89 && c[1] == 0x50 && c[2] == 0x4E && c[3] == 0x47,
            ".webp" => n >= 12 && c[0] == 0x52 && c[1] == 0x49 && c[8] == 0x57 && c[9] == 0x45,
            _ => c[0] == 0xFF && c[1] == 0xD8 && c[2] == 0xFF
        };
    }
}
