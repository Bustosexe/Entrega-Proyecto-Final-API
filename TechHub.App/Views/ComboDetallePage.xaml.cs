using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class ComboDetallePage : ContentPage
{
    private readonly ComboDetalleViewModel _vm;

    public ComboDetallePage(ComboDetalleViewModel vm)
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
