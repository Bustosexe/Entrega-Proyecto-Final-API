namespace TechHub.Api.Models;

/// <summary>Componente de hardware a la venta.</summary>
public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    /// <summary>Por debajo de este valor el producto aparece en la alerta de stock bajo.</summary>
    public int StockMinimo { get; set; } = 2;
    /// <summary>Ruta relativa de la imagen, ej: /uploads/3f2a....jpg</summary>
    public string? ImagenUrl { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public int CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public ICollection<Ingreso> Ingresos { get; set; } = new List<Ingreso>();
    public ICollection<Salida> Salidas { get; set; } = new List<Salida>();
}
