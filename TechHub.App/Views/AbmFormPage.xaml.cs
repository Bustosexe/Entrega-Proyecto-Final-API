using TechHub.App.ViewModels;

namespace TechHub.App.Views;

public partial class AbmFormPage : ContentPage
{
    public AbmFormPage(AbmFormViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
