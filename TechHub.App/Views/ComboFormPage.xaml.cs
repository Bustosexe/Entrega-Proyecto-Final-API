using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class ComboFormPage : ContentPage
{
    private readonly ComboFormViewModel _vm;

    public ComboFormPage(ComboFormViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AlAparecerAsync();
    }

    private void OnQuitar(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: ComboLinea l }) _vm.Quitar(l);
    }

    private void OnMas(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: ComboLinea l }) _vm.Mas(l);
    }

    private void OnMenos(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: ComboLinea l }) _vm.Menos(l);
    }
}
