using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class CuentaPage : ContentPage
{
    private readonly CuentaViewModel _vm;

    public CuentaPage(CuentaViewModel vm)
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
