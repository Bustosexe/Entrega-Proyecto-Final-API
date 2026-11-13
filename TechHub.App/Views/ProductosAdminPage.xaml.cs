using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class ProductosAdminPage : ContentPage
{
    private readonly ProductosAdminViewModel _vm;

    public ProductosAdminPage(ProductosAdminViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AlAparecerAsync();
    }

    private async void OnProductoSeleccionado(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ProductoDto producto) return;
        if (sender is CollectionView lista) lista.SelectedItem = null;
        await _vm.AbrirAsync(producto);
    }
}
