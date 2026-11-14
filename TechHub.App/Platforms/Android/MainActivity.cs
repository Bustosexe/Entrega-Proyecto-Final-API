using Android.App;
using Android.Content.PM;
using Android.OS;

namespace TechHub.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        try
        {
            // Barra de estado blanca con íconos oscuros, a juego con el diseño claro.
            if (Window is not null)
            {
#pragma warning disable CA1422 // SetStatusBarColor está obsoleto en Android 15, donde la app ya se dibuja a pantalla completa.
                Window.SetStatusBarColor(Android.Graphics.Color.White);
#pragma warning restore CA1422
                new AndroidX.Core.View.WindowInsetsControllerCompat(Window, Window.DecorView).AppearanceLightStatusBars = true;
            }
        }
        catch
        {
            // Es solo estética: si el dispositivo no lo soporta, se deja la barra por defecto.
        }
    }
}
