using Microsoft.Maui.Graphics;
using TechHub.App.Models;

namespace TechHub.App.Graficos;

/// <summary>
/// Gráfico de barras dibujado con Microsoft.Maui.Graphics (sin librerías externas).
/// Se usa en el panel del administrador para la facturación de los últimos días.
/// </summary>
public class GraficoBarras(IReadOnlyList<(string Etiqueta, double Valor)> datos) : IDrawable
{
    private static readonly Color ColorBarra = Color.FromArgb("#C7D2FE");
    private static readonly Color ColorBarraHoy = Color.FromArgb("#4F46E5");
    private static readonly Color ColorVacio = Color.FromArgb("#EEF2FF");
    private static readonly Color ColorTexto = Color.FromArgb("#64748B");
    private static readonly Color ColorGuia = Color.FromArgb("#F3F4F6");

    public void Draw(ICanvas canvas, RectF r)
    {
        if (datos.Count == 0) return;

        const float margenInferior = 22f;
        const float margenSuperior = 18f;
        var alturaUtil = r.Height - margenInferior - margenSuperior;
        var maximo = Math.Max(1d, datos.Max(d => d.Valor));
        var anchoColumna = r.Width / datos.Count;

        // Líneas guía horizontales
        canvas.StrokeColor = ColorGuia;
        canvas.StrokeSize = 1;
        for (var i = 0; i <= 2; i++)
        {
            var y = r.Top + margenSuperior + alturaUtil * i / 2f;
            canvas.DrawLine(r.Left, y, r.Right, y);
        }

        canvas.FontSize = 10;
        for (var i = 0; i < datos.Count; i++)
        {
            var (etiqueta, valor) = datos[i];
            var alto = (float)(valor / maximo) * alturaUtil;
            if (alto < 3) alto = 3;

            var x = r.Left + i * anchoColumna + anchoColumna * 0.2f;
            var ancho = anchoColumna * 0.6f;
            var y = r.Bottom - margenInferior - alto;

            canvas.FillColor = valor <= 0 ? ColorVacio : i == datos.Count - 1 ? ColorBarraHoy : ColorBarra;
            canvas.FillRoundedRectangle(x, y, ancho, alto, 6);

            canvas.FontColor = ColorTexto;
            canvas.DrawString(etiqueta, r.Left + i * anchoColumna, r.Bottom - margenInferior + 4, anchoColumna, 16,
                Microsoft.Maui.Graphics.HorizontalAlignment.Center, Microsoft.Maui.Graphics.VerticalAlignment.Top);

            if (valor > 0)
            {
                canvas.FontColor = Color.FromArgb("#0F172A");
                canvas.DrawString(Compacto(valor), r.Left + i * anchoColumna, y - 16, anchoColumna, 14,
                    Microsoft.Maui.Graphics.HorizontalAlignment.Center, Microsoft.Maui.Graphics.VerticalAlignment.Bottom);
            }
        }
    }

    /// <summary>Formato corto para la etiqueta de cada barra: $850k, $1,2M.</summary>
    private static string Compacto(double v) => v switch
    {
        >= 1_000_000 => "$" + (v / 1_000_000).ToString("0.#", Formato.Cultura) + "M",
        >= 1_000 => "$" + (v / 1_000).ToString("0", Formato.Cultura) + "k",
        _ => "$" + v.ToString("0", Formato.Cultura)
    };
}
