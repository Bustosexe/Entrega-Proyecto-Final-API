namespace TechHub.Api.Models;

/// <summary>
/// Cabecera del pedido: quién compra, cuándo, en qué estado está y el comprobante de pago.
/// Los productos están en DetallesPedido y cada cambio de estado queda en HistorialPedidos.
/// </summary>
public class Pedido
{
    public int Id { get; set; }
    /// <summary>Código legible y único, ej: P261113-4F2A9C.</summary>
    public string Codigo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Observaciones { get; set; }
    /// <summary>Nombre del archivo del comprobante de transferencia (carpeta privada App_Data/comprobantes).</summary>
    public string? ComprobanteArchivo { get; set; }

    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    /// <summary>Usuario que registró el pedido (el comprador, o el admin en una venta en el local).</summary>
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public int EstadoId { get; set; } = EstadosPedido.PendienteId;
    public EstadoPedido Estado { get; set; } = null!;

    public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
    public ICollection<HistorialPedido> Historial { get; set; } = new List<HistorialPedido>();
}
