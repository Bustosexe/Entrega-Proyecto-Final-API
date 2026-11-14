namespace TechHub.Api.Models;

/// <summary>Fabricante del producto (AMD, Intel, ASUS, MSI...). Antes era un texto repetido en cada producto.</summary>
public class Marca
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
