using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class MisPedidosPage : ContentPage
{
    private readonly MisPedidosViewModel _vm;

    public MisPedidosPage(MisPedidosViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AlAparecerAsync();
    }

    private async void OnPedidoSeleccionado(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not PedidoDto pedido) return;
        if (sender is CollectionView lista) lista.SelectedItem = null;
        await _vm.AbrirAsync(pedido);
    }
}
