using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class CatalogoPage : ContentPage
{
    private readonly CatalogoViewModel _vm;

    public CatalogoPage(CatalogoViewModel vm)
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
        await _vm.AbrirProductoAsync(producto);
    }

    private async void OnDestacadoTocado(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: ProductoDto producto })
            await _vm.AbrirProductoAsync(producto);
    }

    private async void OnComboTocado(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: ComboDto combo })
            await _vm.AbrirComboAsync(combo);
    }

    private void OnAgregarRapido(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: ProductoDto producto })
            _vm.AgregarRapido(producto);
    }

    private async void OnMarcaTocada(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: MarcaFiltro marca })
            await _vm.SeleccionarMarcaAsync(marca);
    }

    private async void OnCategoriaTocada(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: CategoriaFiltro categoria })
            await _vm.SeleccionarCategoriaAsync(categoria);
    }
}
