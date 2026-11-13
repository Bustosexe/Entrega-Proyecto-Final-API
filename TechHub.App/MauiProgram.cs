using Microsoft.Extensions.Logging;
using TechHub.App.Services;
using TechHub.App.ViewModels;
using TechHub.App.Views;

namespace TechHub.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Servicios (una instancia para toda la app)
        builder.Services.AddSingleton<SesionService>();
        builder.Services.AddSingleton<CarritoService>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<AppShell>();

        // Tienda (visitante / comprador)
        builder.Services.AddTransient<CatalogoPage>().AddTransient<CatalogoViewModel>();
        builder.Services.AddTransient<ProductoDetallePage>().AddTransient<ProductoDetalleViewModel>();
        builder.Services.AddTransient<CarritoPage>().AddTransient<CarritoViewModel>();
        builder.Services.AddTransient<MisPedidosPage>().AddTransient<MisPedidosViewModel>();
        builder.Services.AddTransient<PedidoDetallePage>().AddTransient<PedidoDetalleViewModel>();
        builder.Services.AddTransient<CuentaPage>().AddTransient<CuentaViewModel>();
        builder.Services.AddTransient<LoginPage>().AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegistroPage>().AddTransient<RegistroViewModel>();

        // Administración
        builder.Services.AddTransient<PanelPage>().AddTransient<PanelViewModel>();
        builder.Services.AddTransient<PedidosAdminPage>().AddTransient<PedidosAdminViewModel>();
        builder.Services.AddTransient<ProductosAdminPage>().AddTransient<ProductosAdminViewModel>();
        builder.Services.AddTransient<ProductoFormPage>().AddTransient<ProductoFormViewModel>();
        builder.Services.AddTransient<IngresosPage>().AddTransient<IngresosViewModel>();
        builder.Services.AddTransient<IngresoFormPage>().AddTransient<IngresoFormViewModel>();
        builder.Services.AddTransient<MasPage>();
        builder.Services.AddTransient<AbmListaPage>().AddTransient<AbmListaViewModel>();
        builder.Services.AddTransient<AbmFormPage>().AddTransient<AbmFormViewModel>();

        return builder.Build();
    }
}
