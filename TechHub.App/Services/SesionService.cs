using System.Text.Json;
using TechHub.App.Models;

namespace TechHub.App.Services;

/// <summary>
/// Sesión del usuario. El token JWT se guarda cifrado en SecureStorage (Keystore de Android);
/// los datos no sensibles del usuario, en Preferences para poder leerlos de forma sincrónica.
/// </summary>
public class SesionService
{
    private const string ClaveToken = "th_token";
    private const string ClaveUsuario = "th_usuario";
    private const string ClaveExpira = "th_expira";

    private string? _tokenEnMemoria;

    public event EventHandler? Cambio;

    public UsuarioDto? Usuario { get; private set; }

    public bool EstaLogueado =>
        Usuario is not null && DateTime.UtcNow < new DateTime(Preferences.Default.Get(ClaveExpira, 0L), DateTimeKind.Utc);

    public bool EsAdmin => EstaLogueado && Usuario!.EsAdmin;
    public bool EsComprador => EstaLogueado && !Usuario!.EsAdmin;

    public SesionService()
    {
        var json = Preferences.Default.Get(ClaveUsuario, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            try { Usuario = JsonSerializer.Deserialize<UsuarioDto>(json); }
            catch { Usuario = null; }
        }
    }

    public async Task IniciarAsync(LoginResponse login)
    {
        _tokenEnMemoria = login.Token;
        Usuario = login.Usuario;
        await GuardarTokenAsync(login.Token);
        Preferences.Default.Set(ClaveUsuario, JsonSerializer.Serialize(login.Usuario));
        Preferences.Default.Set(ClaveExpira, login.Expira.ToUniversalTime().Ticks);
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    public void ActualizarUsuario(UsuarioDto usuario)
    {
        Usuario = usuario;
        Preferences.Default.Set(ClaveUsuario, JsonSerializer.Serialize(usuario));
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    public void Cerrar()
    {
        _tokenEnMemoria = null;
        Usuario = null;
        try { SecureStorage.Default.Remove(ClaveToken); } catch { /* sin almacenamiento seguro */ }
        Preferences.Default.Remove(ClaveUsuario);
        Preferences.Default.Remove(ClaveExpira);
        Cambio?.Invoke(this, EventArgs.Empty);
    }

    public async Task<string?> ObtenerTokenAsync()
    {
        if (!EstaLogueado) return null;
        if (_tokenEnMemoria is not null) return _tokenEnMemoria;
        try
        {
            _tokenEnMemoria = await SecureStorage.Default.GetAsync(ClaveToken);
        }
        catch
        {
            // Puede fallar si se restauró un backup o cambió la clave del dispositivo.
            SecureStorage.Default.RemoveAll();
            _tokenEnMemoria = null;
        }
        return _tokenEnMemoria;
    }

    private static async Task GuardarTokenAsync(string token)
    {
        try { await SecureStorage.Default.SetAsync(ClaveToken, token); }
        catch { /* si falla, el token queda solo en memoria durante esta ejecución */ }
    }
}
