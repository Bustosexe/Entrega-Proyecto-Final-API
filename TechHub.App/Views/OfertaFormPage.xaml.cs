using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class OfertaFormPage : ContentPage
{
    public OfertaFormPage(OfertaFormViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
