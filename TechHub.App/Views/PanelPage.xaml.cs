using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class PanelPage : ContentPage
{
    private readonly PanelViewModel _vm;

    public PanelPage(PanelViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        // Cuando llegan datos nuevos, se redibuja el gráfico.
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PanelViewModel.GraficoVentas))
                GraficoVentasView.Invalidate();
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AlAparecerAsync();
    }

    private async void OnProductoTocado(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: ProductoDto producto })
            await _vm.AbrirProductoAsync(producto);
    }
}
