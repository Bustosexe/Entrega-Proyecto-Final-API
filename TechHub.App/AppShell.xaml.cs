using TechHub.App.Services;
using TechHub.App.Views;

namespace TechHub.App;

public partial class AppShell : Shell
{
    public const string RutaTienda = "//tienda/catalogo";
    public const string RutaAdmin = "//admin/panel";

    public AppShell(SesionService sesion)
    {
        InitializeComponent();

        // Pantallas que se abren encima de las pestañas
        Routing.RegisterRoute("producto", typeof(ProductoDetallePage));
        Routing.RegisterRoute("login", typeof(LoginPage));
        Routing.RegisterRoute("registro", typeof(RegistroPage));
        Routing.RegisterRoute("pedido", typeof(PedidoDetallePage));
        Routing.RegisterRoute("productoform", typeof(ProductoFormPage));
        Routing.RegisterRoute("ingresoform", typeof(IngresoFormPage));
        Routing.RegisterRoute("abmlista", typeof(AbmListaPage));
        Routing.RegisterRoute("abmform", typeof(AbmFormPage));

        // Si quedó una sesión de administrador abierta, la app arranca en el panel.
        CurrentItem = sesion.EsAdmin ? TabsAdmin : TabsTienda;
    }

    /// <summary>Lleva al usuario a su sección según el rol después de iniciar sesión.</summary>
    public static Task IrAInicioAsync(SesionService sesion, string? rutaComprador = null) =>
        Current.GoToAsync(sesion.EsAdmin ? RutaAdmin : rutaComprador ?? RutaTienda);
}
