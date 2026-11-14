namespace TechHub.Api.Models;

/// <summary>Estado de un pedido (tabla de referencia). Los valores fijos están en <see cref="EstadosPedido"/>.</summary>
public class EstadoPedido
{
    public int Id { get; set; }
    /// <summary>Código que usa la API y la app: Pendiente, EnRevision, Confirmado, Entregado, Cancelado.</summary>
    public string Codigo { get; set; } = string.Empty;
    /// <summary>Texto para mostrar: "Esperando pago", "Pago en revisión", etc.</summary>
    public string Nombre { get; set; } = string.Empty;

    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    public ICollection<HistorialPedido> Historial { get; set; } = new List<HistorialPedido>();
}
