namespace TechHub.Api.Models;

/// <summary>Compra a proveedor: suma stock. Una fila por producto.</summary>
public class Ingreso
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public int Cantidad { get; set; }
    /// <summary>Costo unitario pagado al proveedor en ese momento.</summary>
    public decimal CostoUnitario { get; set; }
    public decimal Total { get; set; }
    public string? NumeroComprobante { get; set; }
    public string? Observaciones { get; set; }

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    public int ProveedorId { get; set; }
    public Proveedor Proveedor { get; set; } = null!;
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
}
