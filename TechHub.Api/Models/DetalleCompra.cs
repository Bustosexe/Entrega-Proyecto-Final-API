namespace TechHub.Api.Models;

/// <summary>Renglón de una compra: producto, cantidad que ingresa al stock y costo unitario.</summary>
public class DetalleCompra
{
    public int Id { get; set; }

    public int CompraId { get; set; }
    public Compra Compra { get; set; } = null!;

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public int Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
}
