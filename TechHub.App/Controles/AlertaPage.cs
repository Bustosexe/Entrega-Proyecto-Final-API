using Microsoft.Maui.Controls.Shapes;

namespace TechHub.App.Controles;

public enum TipoAlerta { Exito, Error, Advertencia, Pregunta, Info }

/// <summary>
/// Ventana emergente al estilo SweetAlert, hecha con controles nativos de .NET MAUI (sin librerías externas):
/// fondo oscurecido, tarjeta blanca con animación de entrada, ícono circular según el tipo
/// (éxito, error, advertencia, pregunta, info), botones redondeados y, opcionalmente, un campo de texto.
/// </summary>
public class AlertaPage : ContentPage
{
    private static readonly Color Texto = Color.FromArgb("#18181B");
    private static readonly Color TextoSecundario = Color.FromArgb("#6B6B73");

    private readonly TaskCompletionSource<string?> _resultado = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Border _tarjeta;
    private readonly View _icono;
    private readonly Entry? _entrada;
    private readonly bool _tieneCancelar;
    private readonly int _autoCerrarMs;
    private bool _cerrando;

    /// <summary>null = canceló; texto (o vacío) = aceptó.</summary>
    public Task<string?> Resultado => _resultado.Task;

    public AlertaPage(TipoAlerta tipo, string titulo, string? mensaje, string textoConfirmar = "Aceptar",
        string? textoCancelar = null, bool peligro = false, bool conEntrada = false,
        string? placeholder = null, Keyboard? teclado = null, int autoCerrarMs = 0)
    {
        _tieneCancelar = textoCancelar is not null;
        _autoCerrarMs = autoCerrarMs;

        BackgroundColor = Color.FromRgba(30, 27, 75, 140); // fondo oscurecido semitransparente
        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);

        var color = ColorDe(tipo);
        _icono = CrearIcono(tipo, color);
        _icono.Scale = 0.3;

        var contenido = new VerticalStackLayout { Spacing = 10 };
        contenido.Add(_icono);
        contenido.Add(new Label
        {
            Text = titulo,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Texto,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0)
        });
        if (!string.IsNullOrWhiteSpace(mensaje))
        {
            contenido.Add(new Label
            {
                Text = mensaje,
                FontSize = 15,
                TextColor = TextoSecundario,
                HorizontalTextAlignment = TextAlignment.Center
            });
        }

        if (conEntrada)
        {
            _entrada = new Entry
            {
                Placeholder = placeholder,
                Keyboard = teclado ?? Keyboard.Default,
                FontSize = 15,
                TextColor = Texto,
                PlaceholderColor = Color.FromArgb("#9CA3AF"),
                ReturnType = ReturnType.Done
            };
            _entrada.Completed += async (_, _) => await CerrarAsync(_entrada.Text?.Trim() ?? string.Empty);
            contenido.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#F0F0F1"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(12, 0),
                Margin = new Thickness(0, 8, 0, 0),
                Content = _entrada
            });
        }

        // Botones
        var confirmar = CrearBoton(textoConfirmar, peligro ? Color.FromArgb("#B91C1C") : Color.FromArgb("#D63224"), Colors.White);
        confirmar.Clicked += async (_, _) => await CerrarAsync(_entrada?.Text?.Trim() ?? string.Empty);

        var botones = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 18, 0, 0) };
        if (textoCancelar is not null)
        {
            botones.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            botones.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var cancelar = CrearBoton(textoCancelar, Color.FromArgb("#FDECEA"), Color.FromArgb("#D63224"));
            cancelar.Clicked += async (_, _) => await CerrarAsync(null);
            botones.Add(cancelar, 0, 0);
            botones.Add(confirmar, 1, 0);
        }
        else
        {
            botones.Add(confirmar);
        }
        contenido.Add(botones);

        _tarjeta = new Border
        {
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 28 },
            Padding = new Thickness(24, 30, 24, 22),
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 420,
            Content = contenido,
            Scale = 0.85,
            Opacity = 0
        };

        Content = new Grid { Padding = new Thickness(28), Children = { _tarjeta } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.WhenAll(_tarjeta.FadeTo(1, 160), _tarjeta.ScaleTo(1, 300, Easing.SpringOut));
        await _icono.ScaleTo(1, 380, Easing.SpringOut);
        _entrada?.Focus();

        if (_autoCerrarMs > 0)
        {
            await Task.Delay(_autoCerrarMs);
            await CerrarAsync(string.Empty);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Si el sistema cerró la ventana por otro camino, se libera igual a quien espera la respuesta.
        // Solo si NO la cerró un botón: en ese caso CerrarAsync entrega la respuesta real.
        if (!_cerrando)
            _resultado.TrySetResult(_tieneCancelar ? null : string.Empty);
    }

    /// <summary>El botón "atrás" de Android equivale a Cancelar (o a Aceptar si no hay botón Cancelar).</summary>
    protected override bool OnBackButtonPressed()
    {
        _ = CerrarAsync(_tieneCancelar ? null : string.Empty);
        return true;
    }

    private async Task CerrarAsync(string? resultado)
    {
        if (_cerrando) return;
        _cerrando = true;
        _entrada?.Unfocus();

        try
        {
            await Task.WhenAll(_tarjeta.FadeTo(0, 120), _tarjeta.ScaleTo(0.92, 120));
            // En algunos dispositivos el cierre del modal tarda o no avisa que terminó:
            // se espera como máximo 800 ms para que la respuesta nunca quede trabada.
            if (Navigation.ModalStack.Contains(this))
                await Task.WhenAny(Navigation.PopModalAsync(false), Task.Delay(800));
        }
        catch
        {
            // ya estaba cerrada
        }
        finally
        {
            _resultado.TrySetResult(resultado);
        }
    }

    // -----------------------------------------------------------------

    private static Color ColorDe(TipoAlerta tipo) => tipo switch
    {
        TipoAlerta.Exito => Color.FromArgb("#16A34A"),
        TipoAlerta.Error => Color.FromArgb("#B91C1C"),
        TipoAlerta.Advertencia => Color.FromArgb("#F59E0B"),
        TipoAlerta.Pregunta => Color.FromArgb("#52525B"),
        _ => Color.FromArgb("#3B82F6")
    };

    private static View CrearIcono(TipoAlerta tipo, Color color)
    {
        View simbolo = tipo switch
        {
            TipoAlerta.Exito => Trazo("M 8,21 L 17,30 L 33,11", color),
            TipoAlerta.Error => Trazo("M 11,11 L 29,29 M 29,11 L 11,29", color),
            TipoAlerta.Advertencia => Simbolo("!", color),
            TipoAlerta.Pregunta => Simbolo("?", color),
            _ => Simbolo("i", color)
        };

        return new Border
        {
            WidthRequest = 84,
            HeightRequest = 84,
            StrokeShape = new RoundRectangle { CornerRadius = 42 },
            Stroke = color,
            StrokeThickness = 4,
            BackgroundColor = color.WithAlpha(0.08f),
            HorizontalOptions = LayoutOptions.Center,
            Content = simbolo
        };
    }

    private static View Trazo(string datos, Color color) => new Microsoft.Maui.Controls.Shapes.Path
    {
        Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(datos)!,
        Stroke = new SolidColorBrush(color),
        StrokeThickness = 5,
        StrokeLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        WidthRequest = 40,
        HeightRequest = 40,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center
    };

    private static View Simbolo(string texto, Color color) => new Label
    {
        Text = texto,
        FontSize = 42,
        FontAttributes = FontAttributes.Bold,
        TextColor = color,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center
    };

    private static Button CrearBoton(string texto, Color fondo, Color colorTexto) => new()
    {
        Text = texto,
        BackgroundColor = fondo,
        TextColor = colorTexto,
        CornerRadius = 16,
        HeightRequest = 50,
        FontSize = 15,
        FontAttributes = FontAttributes.Bold
    };
}
