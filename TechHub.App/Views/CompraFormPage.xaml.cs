using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class CompraFormPage : ContentPage
{
    private readonly CompraFormViewModel _vm;

    public CompraFormPage(CompraFormViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AlAparecerAsync();
    }

    private void OnQuitarRenglon(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: RenglonCompra renglon })
            _vm.QuitarRenglon(renglon);
    }
}
