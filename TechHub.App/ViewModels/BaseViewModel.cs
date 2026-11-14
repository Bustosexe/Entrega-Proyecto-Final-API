using CommunityToolkit.Mvvm.ComponentModel;
using TechHub.App.Services;

namespace TechHub.App.ViewModels;

/// <summary>
/// Diálogos centralizados. Las alertas, confirmaciones y pedidos de texto usan la ventana estilo
/// SweetAlert (Controles/AlertaPage); los avisos breves usan el Toast nativo de Android.
/// </summary>
public static class Dialogos
{
    private static readonly SemaphoreSlim Cola = new(1, 1);

    private static Page? PaginaActual => Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page;

    /// <summary>Muestra una alerta a la vez (si llegan dos, la segunda espera a que se cierre la primera).</summary>
    private static async Task<string?> MostrarAsync(Func<AlertaPage> crear)
    {
        // Nunca se espera indefinidamente a otra alerta: como máximo 3 segundos.
        var tomado = await Cola.WaitAsync(TimeSpan.FromSeconds(3));
        try
        {
            var navegacion = Shell.Current?.Navigation ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
            if (navegacion is null) return null;

            var pagina = await MainThread.InvokeOnMainThreadAsync(crear);
            await MainThread.InvokeOnMainThreadAsync(() => navegacion.PushModalAsync(pagina, false));
            return await pagina.Resultado;
        }
        catch (Exception ex)
        {
            // Si la ventana no se pudo mostrar, se registra el error y la app sigue (nunca queda trabada).
            System.Diagnostics.Debug.WriteLine($"[Dialogos] {ex}");
            return null;
        }
        finally
        {
            if (tomado) Cola.Release();
        }
    }

    /// <summary>Alerta con un botón. Por defecto de tipo información.</summary>
    public static Task Alerta(string titulo, string mensaje, TipoAlerta tipo = TipoAlerta.Info) =>
        MostrarAsync(() => new AlertaPage(tipo, titulo, mensaje));

    /// <summary>Alerta de advertencia (datos incompletos, sin stock...).</summary>
    public static Task Advertencia(string titulo, string mensaje) => Alerta(titulo, mensaje, TipoAlerta.Advertencia);

    /// <summary>Éxito con el tilde animado. Con autoCerrarMs se cierra sola (como el "timer" de SweetAlert).</summary>
    public static Task Exito(string titulo, string mensaje, int autoCerrarMs = 0) =>
        MostrarAsync(() => new AlertaPage(TipoAlerta.Exito, titulo, mensaje, "Listo", autoCerrarMs: autoCerrarMs));

    /// <summary>Pregunta Sí/No. Con peligro = true el botón de confirmar es rojo (acciones destructivas).</summary>
    public static async Task<bool> Confirmar(string titulo, string mensaje, string aceptar = "Sí", string cancelar = "No",
        bool peligro = false) =>
        await MostrarAsync(() => new AlertaPage(peligro ? TipoAlerta.Advertencia : TipoAlerta.Pregunta,
            titulo, mensaje, aceptar, cancelar, peligro)) is not null;

    /// <summary>Aviso breve que no interrumpe (Toast nativo de Android).</summary>
    public static void Aviso(string mensaje)
    {
#if ANDROID
        MainThread.BeginInvokeOnMainThread(() =>
            Android.Widget.Toast.MakeText(Microsoft.Maui.ApplicationModel.Platform.AppContext, mensaje, Android.Widget.ToastLength.Short)?.Show());
#else
        _ = Alerta("TechHub", mensaje);
#endif
    }

    /// <summary>Menú de opciones (hoja de acciones). Devuelve la opción elegida o null.</summary>
    public static async Task<string?> Opciones(string titulo, params string[] opciones)
    {
        if (PaginaActual is not { } pagina) return null;
        var r = await pagina.DisplayActionSheet(titulo, "Cancelar", null, opciones);
        return r is null || r == "Cancelar" ? null : r;
    }

    /// <summary>Pide un texto al usuario (motivo, email) en una alerta con campo de texto. Devuelve null si cancela.</summary>
    public static Task<string?> Pedir(string titulo, string mensaje, Keyboard? teclado = null, string? placeholder = null) =>
        MostrarAsync(() => new AlertaPage(TipoAlerta.Pregunta, titulo, mensaje, "Aceptar", "Cancelar",
            conEntrada: true, placeholder: placeholder, teclado: teclado));

    public static Task Error(Exception ex) =>
        Alerta("Ups...", ex is ApiException ? ex.Message : $"Ocurrió un error inesperado: {ex.Message}", TipoAlerta.Error);
}

/// <summary>Base de los ViewModels: estado de carga y ejecución segura con manejo de errores.</summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoEstaOcupado))]
    private bool estaOcupado;

    [ObservableProperty]
    private bool estaRefrescando;

    [ObservableProperty]
    private string titulo = string.Empty;

    public bool NoEstaOcupado => !EstaOcupado;

    private CancellationTokenSource? _debounce;

    /// <summary>Espera a que el usuario deje de escribir antes de buscar (evita una consulta por cada tecla).</summary>
    protected void Demorar(Func<Task> accion, int milisegundos = 450)
    {
        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Task.Delay(milisegundos, token);
                // Si hay una carga en curso, espera a que termine para no perder la búsqueda.
                while (EstaOcupado && !token.IsCancellationRequested)
                    await Task.Delay(100, token);
                if (!token.IsCancellationRequested)
                    await accion();
            }
            catch (TaskCanceledException) { }
        });
    }

    protected static Task IrAsync(string ruta, IDictionary<string, object>? parametros = null) =>
        parametros is null ? Shell.Current.GoToAsync(ruta) : Shell.Current.GoToAsync(ruta, parametros);

    /// <summary>Ejecuta una acción mostrando el indicador de carga y un mensaje si falla.</summary>
    protected async Task<bool> EjecutarAsync(Func<Task> accion)
    {
        // Si hay otra carga en curso, se espera a que termine (hasta 10 s) en vez de ignorar la acción.
        // Ej.: al cerrarse una alerta, la pantalla de abajo vuelve a aparecer y recarga sus datos;
        // sin esta espera, la acción que el usuario acaba de confirmar ("Aprobar pago") se descartaba.
        for (var i = 0; EstaOcupado && i < 100; i++)
            await Task.Delay(100);
        if (EstaOcupado) return false;

        try
        {
            EstaOcupado = true;
            await accion();
            return true;
        }
        catch (Exception ex)
        {
            await Dialogos.Error(ex);
            return false;
        }
        finally
        {
            EstaOcupado = false;
            EstaRefrescando = false;
        }
    }
}
