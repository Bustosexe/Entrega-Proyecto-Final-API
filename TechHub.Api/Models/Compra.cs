namespace TechHub.Api.Models;

/// <summary>Cabecera de una compra a un proveedor (una factura). Los productos están en DetallesCompra.</summary>
public class Compra
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    /// <summary>Número de la factura o remito del proveedor.</summary>
    public string? NumeroComprobante { get; set; }
    public string? Observaciones { get; set; }

    public int ProveedorId { get; set; }
    public Proveedor Proveedor { get; set; } = null!;

    /// <summary>Administrador que registró la compra.</summary>
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public ICollection<DetalleCompra> Detalles { get; set; } = new List<DetalleCompra>();
}
