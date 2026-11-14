namespace TechHub.Api.Models;

/// <summary>
/// Venta a cliente: descuenta (reserva) stock. Una fila por producto.
/// Las filas de una misma compra comparten CodigoPedido, Estado y datos del comprobante de pago.
/// </summary>
public class Salida
{
    public int Id { get; set; }
    /// <summary>Agrupa los productos de una misma compra. Ej: P261113-4F2A9C</summary>
    public string CodigoPedido { get; set; } = string.Empty;
    public string Estado { get; set; } = EstadosPedido.Pendiente;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public int Cantidad { get; set; }
    /// <summary>Precio de venta congelado al momento de la operación.</summary>
    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }
    public string? Observaciones { get; set; }

    /// <summary>Archivo del comprobante de transferencia (carpeta privada App_Data/comprobantes).</summary>
    public string? ComprobanteArchivo { get; set; }
    public DateTime? ComprobanteFecha { get; set; }
    /// <summary>Motivo informado por el admin cuando rechaza un comprobante.</summary>
    public string? MotivoRechazo { get; set; }

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    /// <summary>Quién registró la operación (el comprador desde la app o el admin en el local).</summary>
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
}
