using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class ReportesPage : ContentPage
{
    private readonly ReportesViewModel _vm;

    public ReportesPage(ReportesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.AlAparecer();
    }
}
