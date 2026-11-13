using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class ProductoFormPage : ContentPage
{
    private readonly ProductoFormViewModel _vm;

    public ProductoFormPage(ProductoFormViewModel vm)
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
