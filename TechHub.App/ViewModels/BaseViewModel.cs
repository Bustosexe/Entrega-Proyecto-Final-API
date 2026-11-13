using CommunityToolkit.Mvvm.ComponentModel;
using TechHub.App.Services;

namespace TechHub.App.ViewModels;

/// <summary>Diálogos centralizados (alertas, confirmaciones y avisos breves).</summary>
public static class Dialogos
{
    private static Page? PaginaActual => Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page;

    public static Task Alerta(string titulo, string mensaje) =>
        PaginaActual?.DisplayAlert(titulo, mensaje, "Aceptar") ?? Task.CompletedTask;

    public static Task<bool> Confirmar(string titulo, string mensaje, string aceptar = "Sí", string cancelar = "No") =>
        PaginaActual?.DisplayAlert(titulo, mensaje, aceptar, cancelar) ?? Task.FromResult(false);

    public static Task Error(Exception ex) =>
        Alerta("Atención", ex is ApiException ? ex.Message : $"Ocurrió un error inesperado: {ex.Message}");
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
