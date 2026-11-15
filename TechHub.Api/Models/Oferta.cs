namespace TechHub.Api.Models;

/// <summary>
/// Descuento temporal sobre un producto. El precio final se calcula con el porcentaje (no se guarda),
/// así que si el precio de lista cambia, la oferta sigue siendo correcta.
/// </summary>
public class Oferta
{
    public int Id { get; set; }

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    /// <summary>Nombre visible en el catálogo, ej: "Hot Sale".</summary>
    public string? Titulo { get; set; }

    /// <summary>Descuento entre 1 y 90 %.</summary>
    public int Porcentaje { get; set; }

    /// <summary>Inicio de la vigencia (UTC).</summary>
    public DateTime Desde { get; set; }

    /// <summary>Fin de la vigencia (UTC), inclusive.</summary>
    public DateTime Hasta { get; set; }

    /// <summary>Permite pausar la oferta sin borrarla.</summary>
    public bool Activa { get; set; } = true;
}
