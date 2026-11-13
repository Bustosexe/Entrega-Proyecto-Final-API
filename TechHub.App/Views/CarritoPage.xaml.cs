using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class CarritoPage : ContentPage
{
    private readonly CarritoViewModel _vm;

    public CarritoPage(CarritoViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.ActualizarTotales();
    }

    private static CarritoItem? Item(object? sender) => (sender as BindableObject)?.BindingContext as CarritoItem;

    private void OnSumar(object? sender, EventArgs e)
    {
        if (Item(sender) is { } item) _vm.Sumar(item);
    }

    private void OnRestar(object? sender, EventArgs e)
    {
        if (Item(sender) is { } item) _vm.Restar(item);
    }

    private async void OnQuitar(object? sender, EventArgs e)
    {
        if (Item(sender) is { } item) await _vm.QuitarAsync(item);
    }
}
