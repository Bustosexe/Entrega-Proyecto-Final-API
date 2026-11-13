using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class PedidoDetallePage : ContentPage
{
    private readonly PedidoDetalleViewModel _vm;

    public PedidoDetallePage(PedidoDetalleViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AlAparecerAsync();
    }
}
