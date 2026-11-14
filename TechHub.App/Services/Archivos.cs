namespace TechHub.App.Services;

public record ArchivoDescargado(byte[] Contenido, string Nombre, string Mime);

/// <summary>Guarda archivos descargados en la caché de la app y los abre o comparte con las apps del teléfono.</summary>
public static class Archivos
{
    private static async Task<string> GuardarAsync(ArchivoDescargado archivo)
    {
        var nombre = string.Concat(archivo.Nombre.Split(Path.GetInvalidFileNameChars()));
        var ruta = Path.Combine(FileSystem.Current.CacheDirectory, nombre);
        await File.WriteAllBytesAsync(ruta, archivo.Contenido);
        return ruta;
    }

    /// <summary>Abre el archivo con el visor del teléfono (PDF, imagen).</summary>
    public static async Task AbrirAsync(ArchivoDescargado archivo)
    {
        var ruta = await GuardarAsync(archivo);
        await Launcher.Default.OpenAsync(new OpenFileRequest(archivo.Nombre, new ReadOnlyFile(ruta, archivo.Mime)));
    }

    /// <summary>Abre el menú "Compartir" de Android (WhatsApp, Gmail, Drive...).</summary>
    public static async Task CompartirAsync(ArchivoDescargado archivo, string titulo)
    {
        var ruta = await GuardarAsync(archivo);
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = titulo,
            File = new ShareFile(ruta, archivo.Mime)
        });
    }
}
