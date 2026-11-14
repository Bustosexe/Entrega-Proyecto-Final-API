using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class ComprasPage : ContentPage
{
    private readonly ComprasViewModel _vm;

    public ComprasPage(ComprasViewModel vm)
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
