using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class ProductoDetallePage : ContentPage
{
    private readonly ProductoDetalleViewModel _vm;

    public ProductoDetallePage(ProductoDetalleViewModel vm)
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
