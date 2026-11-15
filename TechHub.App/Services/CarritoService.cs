using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using TechHub.App.Models;

namespace TechHub.App.Services;

/// <summary>Producto (o combo) dentro del carrito. Si ComboId tiene valor, el renglón es un combo.</summary>
public partial class CarritoItem : ObservableObject
{
    public int ProductoId { get; set; }
    /// <summary>Id del combo (null si es un producto suelto). Los combos se cobran con el descuento del combo.</summary>
    public int? ComboId { get; set; }
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
    public string PrecioTexto => ComboId is null ? $"{Formato.Moneda(Precio)} c/u" : $"Combo · {Formato.Moneda(Precio)} c/u";
    public string ImagenCompleta => Config.ApiConfig.UrlCompleta(ImagenUrl);
}

/// <summary>Carrito de compras: vive en memoria y se guarda en el teléfono para no perderlo al cerrar la app.</summary>
public class CarritoService
{
    private const string Clave = "th_carrito";
    public const int MaximoPorProducto = 100;
    public const int MaximoPorCombo = 10;

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
        var item = Items.FirstOrDefault(i => i.ComboId is null && i.ProductoId == p.Id);
        var nueva = (item?.Cantidad ?? 0) + cantidad;
        if (nueva > p.Stock || nueva > MaximoPorProducto) return false;

        if (item is null)
        {
            Items.Add(new CarritoItem
            {
                ProductoId = p.Id,
                Nombre = p.Nombre,
                ImagenUrl = p.ImagenUrl,
                Precio = p.PrecioVigente,
                Stock = p.Stock,
                Cantidad = cantidad
            });
        }
        else
        {
            item.Precio = p.PrecioVigente;
            item.Stock = p.Stock;
            item.Cantidad = nueva;
        }
        Guardar();
        return true;
    }

    /// <summary>Agrega un combo completo. Devuelve false si no alcanza el stock de alguno de sus productos.</summary>
    public bool AgregarCombo(ComboDto combo, int cantidad)
    {
        var item = Items.FirstOrDefault(i => i.ComboId == combo.Id);
        var nueva = (item?.Cantidad ?? 0) + cantidad;
        if (nueva > combo.StockDisponible || nueva > MaximoPorCombo) return false;

        if (item is null)
        {
            Items.Add(new CarritoItem
            {
                ComboId = combo.Id,
                Nombre = combo.Nombre,
                ImagenUrl = combo.ImagenUrl,
                Precio = combo.PrecioCombo,
                Stock = combo.StockDisponible,
                Cantidad = cantidad
            });
        }
        else
        {
            item.Precio = combo.PrecioCombo;
            item.Stock = combo.StockDisponible;
            item.Cantidad = nueva;
        }
        Guardar();
        return true;
    }

    public void CambiarCantidad(CarritoItem item, int delta)
    {
        var nueva = item.Cantidad + delta;
        if (nueva <= 0) { Quitar(item); return; }
        if (nueva > item.Stock || nueva > (item.ComboId is null ? MaximoPorProducto : MaximoPorCombo)) return;
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
