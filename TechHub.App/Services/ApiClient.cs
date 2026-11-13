using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TechHub.App.Config;

namespace TechHub.App.Services;

/// <summary>Error devuelto por la API (o de conexión), con un mensaje listo para mostrar.</summary>
public class ApiException(int status, string mensaje) : Exception(mensaje)
{
    public int Status { get; } = status;
}

/// <summary>
/// Cliente HTTP de la API REST. Agrega el header "Authorization: Bearer {token}",
/// convierte las respuestas JSON y traduce los ProblemDetails de error a mensajes en español.
/// </summary>
public class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly SesionService _sesion;

    public ApiClient(SesionService sesion)
    {
        _sesion = sesion;
        _http = new HttpClient
        {
            BaseAddress = new Uri(ApiConfig.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Task<T> GetAsync<T>(string url) => EnviarAsync<T>(HttpMethod.Get, url, null);

    public Task<T> PostAsync<T>(string url, object? cuerpo) => EnviarAsync<T>(HttpMethod.Post, url, cuerpo);

    public Task<T> PutAsync<T>(string url, object? cuerpo) => EnviarAsync<T>(HttpMethod.Put, url, cuerpo);

    public async Task PostAsync(string url, object? cuerpo) => await EnviarSinRespuestaAsync(HttpMethod.Post, url, cuerpo);

    public async Task PutAsync(string url, object? cuerpo) => await EnviarSinRespuestaAsync(HttpMethod.Put, url, cuerpo);

    public async Task DeleteAsync(string url) => await EnviarSinRespuestaAsync(HttpMethod.Delete, url, null);

    /// <summary>Sube un archivo como multipart/form-data (campo "archivo").</summary>
    public async Task<T> SubirArchivoAsync<T>(string url, Stream contenido, string nombreArchivo)
    {
        var extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();
        var mime = extension switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

        using var form = new MultipartFormDataContent();
        var archivo = new StreamContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(archivo, "archivo", nombreArchivo);

        using var req = new HttpRequestMessage(HttpMethod.Post, url.TrimStart('/')) { Content = form };
        using var resp = await SendAsync(req, url);
        return await LeerAsync<T>(resp);
    }

    // ---------------------------------------------------------------------

    private async Task<T> EnviarAsync<T>(HttpMethod metodo, string url, object? cuerpo)
    {
        using var req = CrearRequest(metodo, url, cuerpo);
        using var resp = await SendAsync(req, url);
        return await LeerAsync<T>(resp);
    }

    private async Task EnviarSinRespuestaAsync(HttpMethod metodo, string url, object? cuerpo)
    {
        using var req = CrearRequest(metodo, url, cuerpo);
        using var resp = await SendAsync(req, url);
    }

    private static HttpRequestMessage CrearRequest(HttpMethod metodo, string url, object? cuerpo)
    {
        var req = new HttpRequestMessage(metodo, url.TrimStart('/'));
        if (cuerpo is not null)
            req.Content = JsonContent.Create(cuerpo, cuerpo.GetType(), options: Json);
        return req;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, string url)
    {
        var token = await _sesion.ObtenerTokenAsync();
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req);
        }
        catch (TaskCanceledException)
        {
            throw new ApiException(0, "El servidor tardó demasiado en responder. Intentá de nuevo.");
        }
        catch (HttpRequestException)
        {
            throw new ApiException(0, $"No se pudo conectar con el servidor ({ApiConfig.BaseUrl}). Revisá tu conexión.");
        }

        if (resp.IsSuccessStatusCode)
            return resp;

        using (resp)
        {
            var mensaje = await LeerErrorAsync(resp);

            if (resp.StatusCode == HttpStatusCode.Unauthorized && token is not null && !url.Contains("auth/login"))
            {
                _sesion.Cerrar();
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try { await Shell.Current.GoToAsync("//tienda/cuenta"); } catch { /* navegación opcional */ }
                });
                throw new ApiException(401, "Tu sesión expiró. Volvé a iniciar sesión.");
            }

            throw new ApiException((int)resp.StatusCode, mensaje);
        }
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.NoContent || resp.Content.Headers.ContentLength == 0)
            return default!;
        var datos = await resp.Content.ReadFromJsonAsync<T>(Json);
        return datos!;
    }

    /// <summary>Convierte un ProblemDetails (RFC 7807) en un mensaje legible.</summary>
    private static async Task<string> LeerErrorAsync(HttpResponseMessage resp)
    {
        var status = (int)resp.StatusCode;
        try
        {
            var texto = await resp.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(texto))
            {
                using var doc = JsonDocument.Parse(texto);
                var raiz = doc.RootElement;

                if (raiz.TryGetProperty("errors", out var errores) && errores.ValueKind == JsonValueKind.Object)
                {
                    var mensajes = errores.EnumerateObject()
                        .SelectMany(p => p.Value.ValueKind == JsonValueKind.Array
                            ? p.Value.EnumerateArray().Select(e => e.GetString() ?? "")
                            : Enumerable.Empty<string>())
                        .Where(m => m.Length > 0)
                        .ToList();
                    if (mensajes.Count > 0) return string.Join("\n", mensajes);
                }

                if (raiz.TryGetProperty("detail", out var detalle) && detalle.ValueKind == JsonValueKind.String)
                    return detalle.GetString()!;
                if (raiz.TryGetProperty("title", out var titulo) && titulo.ValueKind == JsonValueKind.String)
                    return titulo.GetString()!;
            }
        }
        catch (JsonException) { /* la respuesta no era JSON */ }

        return status switch
        {
            401 => "Usuario o contraseña incorrectos.",
            403 => "No tenés permisos para realizar esta acción.",
            404 => "No se encontró el recurso solicitado.",
            409 => "La operación entra en conflicto con los datos actuales.",
            >= 500 => "Error en el servidor. Intentá más tarde.",
            _ => $"Error {status}."
        };
    }
}
