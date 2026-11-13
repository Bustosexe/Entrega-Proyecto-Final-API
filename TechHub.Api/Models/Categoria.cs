namespace TechHub.Api.Models;

/// <summary>Clasificación del stock (Tarjetas Gráficas, Procesadores, etc.).</summary>
public class Categoria
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
