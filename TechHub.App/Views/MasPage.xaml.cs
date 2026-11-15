using TechHub.App.Config;
using TechHub.App.ViewModels;

namespace TechHub.App.Views;

/// <summary>Menú del administrador: accesos a los ABM y cierre de sesión.</summary>
public partial class MasPage : ContentPage
{
    private readonly SesionService _sesion;

    public MasPage(SesionService sesion)
    {
        InitializeComponent();
        _sesion = sesion;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LblNombre.Text = _sesion.Usuario?.NombreCompleto ?? "Administrador";
        LblEmail.Text = _sesion.Usuario?.Email ?? string.Empty;
        LblServidor.Text = $"Servidor: {ApiConfig.BaseUrl}";
    }

    private static Task AbrirAbm(string tipo) =>
        Shell.Current.GoToAsync("abmlista", new Dictionary<string, object> { ["Tipo"] = tipo });

    private async void OnReportes(object? sender, EventArgs e) => await Shell.Current.GoToAsync("reportes");
    private async void OnCategorias(object? sender, EventArgs e) => await AbrirAbm("categorias");
    private async void OnMarcas(object? sender, EventArgs e) => await AbrirAbm("marcas");
    private async void OnProveedores(object? sender, EventArgs e) => await AbrirAbm("proveedores");
    private async void OnClientes(object? sender, EventArgs e) => await AbrirAbm("clientes");
    private async void OnOfertas(object? sender, EventArgs e) => await AbrirAbm("ofertas");
    private async void OnCombos(object? sender, EventArgs e) => await AbrirAbm("combos");
    private async void OnAuditoria(object? sender, EventArgs e) => await AbrirAbm("auditoria");
    private async void OnUsuarios(object? sender, EventArgs e) => await AbrirAbm("usuarios");

    private async void OnCerrarSesion(object? sender, EventArgs e)
    {
        if (!await Dialogos.Confirmar("Cerrar sesión", "¿Querés cerrar la sesión de administrador?", "Cerrar sesión", "Cancelar")) return;
        _sesion.Cerrar();
        await Shell.Current.GoToAsync(AppShell.RutaTienda);
    }
}
