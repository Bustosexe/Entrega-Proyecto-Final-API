using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class AbmListaPage : ContentPage
{
    private readonly AbmListaViewModel _vm;

    public AbmListaPage(AbmListaViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.AlAparecerAsync();
    }

    private async void OnItemSeleccionado(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not AbmItem item) return;
        if (sender is CollectionView lista) lista.SelectedItem = null;
        await _vm.AbrirAsync(item);
    }
}
