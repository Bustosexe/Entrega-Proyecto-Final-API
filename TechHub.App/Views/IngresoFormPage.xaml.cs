using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class IngresoFormPage : ContentPage
{
    private readonly IngresoFormViewModel _vm;

    public IngresoFormPage(IngresoFormViewModel vm)
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
