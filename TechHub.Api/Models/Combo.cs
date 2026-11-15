namespace TechHub.Api.Models;

/// <summary>
/// Paquete de productos compatibles entre sí (ej: motherboard + memorias RAM) con un descuento por llevarlos juntos.
/// Los productos y cantidades están en ComboItems.
/// </summary>
public class Combo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Descuento sobre la suma de los precios de lista (1 a 50 %).</summary>
    public int Porcentaje { get; set; }

    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public ICollection<ComboItem> Items { get; set; } = new List<ComboItem>();
}

/// <summary>Un producto del combo y cuántas unidades incluye.</summary>
public class ComboItem
{
    public int Id { get; set; }

    public int ComboId { get; set; }
    public Combo Combo { get; set; } = null!;

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public int Cantidad { get; set; } = 1;
}

/// <summary>
/// Regla de compatibilidad: dos categorías que pueden ir juntas en un combo
/// (motherboard + RAM sí; RAM + fuente de poder no). Siempre CategoriaAId &lt; CategoriaBId.
/// </summary>
public class CategoriaCompatible
{
    public int CategoriaAId { get; set; }
    public Categoria CategoriaA { get; set; } = null!;

    public int CategoriaBId { get; set; }
    public Categoria CategoriaB { get; set; } = null!;
}
