using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class IngresosPage : ContentPage
{
    private readonly IngresosViewModel _vm;

    public IngresosPage(IngresosViewModel vm)
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
