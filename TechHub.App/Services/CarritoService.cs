using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using TechHub.App.Models;

namespace TechHub.App.Services;

/// <summary>Producto dentro del carrito.</summary>
public partial class CarritoItem : ObservableObject
{
    public int ProductoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? ImagenUrl { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    [NotifyPropertyChangedFor(nameof(SubtotalTexto))]
    private int cantidad;

    public decimal Subtotal => Precio * Cantidad;
    public string SubtotalTexto => Formato.Moneda(Subtotal);
    public string PrecioTexto => $"{Formato.Moneda(Precio)} c/u";
    public string ImagenCompleta => Config.ApiConfig.UrlCompleta(ImagenUrl);
}

/// <summary>Carrito de compras: vive en memoria y se guarda en el teléfono para no perderlo al cerrar la app.</summary>
public class CarritoService
{
    private const string Clave = "th_carrito";
    public const int MaximoPorProducto = 100;

    public ObservableCollection<CarritoItem> Items { get; } = [];

    public event EventHandler? Cambio;

    public decimal Total => Items.Sum(i => i.Subtotal);
    public int Unidades => Items.Sum(i => i.Cantidad);
    public bool EstaVacio => Items.Count == 0;

    public CarritoService()
    {
        try
        {
            var json = Preferences.Default.Get(Clave, string.Empty);
            if (!string.IsNullOrEmpty(json))
                foreach (var item in JsonSerializer.Deserialize<List<CarritoItem>>(json) ?? [])
                    Items.Add(item);
        }
        catch { Preferences.Default.Remove(Clave); }
    }

    /// <summary>Agrega unidades. Devuelve false si se supera el stock disponible.</summary>
    public bool Agregar(ProductoDto p, int cantidad)
    {
        var item = Items.FirstOrDefault(i => i.ProductoId == p.Id);
        var nueva = (item?.Cantidad ?? 0) + cantidad;
        if (nueva > p.Stock || nueva > MaximoPorProducto) return false;

        if (item is null)
        {
            Items.Add(new CarritoItem
            {
                ProductoId = p.Id,
                Nombre = p.Nombre,
                ImagenUrl = p.ImagenUrl,
                Precio = p.Precio,
                Stock = p.Stock,
                Cantidad = cantidad
            });
        }
        else
        {
            item.Precio = p.Precio;
            item.Stock = p.Stock;
            item.Cantidad = nueva;
        }
        Guardar();
        return true;
    }

    public void CambiarCantidad(CarritoItem item, int delta)
    {
        var nueva = item.Cantidad + delta;
        if (nueva <= 0) { Quitar(item); return; }
        if (nueva > item.Stock || nueva > MaximoPorProducto) return;
        item.Cantidad = nueva;
        Guardar();
    }

    public void Quitar(CarritoItem item)
    {
        Items.Remove(item);
        Guardar();
    }

    public void Vaciar()
    {
        Items.Clear();
        Guardar();
    }

    private void Guardar()
    {
        Preferences.Default.Set(Clave, JsonSerializer.Serialize(Items.ToList()));
        Cambio?.Invoke(this, EventArgs.Empty);
    }
}
