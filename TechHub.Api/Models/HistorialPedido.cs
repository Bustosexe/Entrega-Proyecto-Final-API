namespace TechHub.Api.Models;

/// <summary>
/// Cada cambio de estado de un pedido: a qué estado pasó, cuándo, quién lo hizo y por qué
/// (por ejemplo, el motivo de un pago rechazado). Alimenta la línea de tiempo y la auditoría.
/// </summary>
public class HistorialPedido
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;

    public int EstadoId { get; set; }
    public EstadoPedido Estado { get; set; } = null!;

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    /// <summary>Quién hizo el cambio. Null en registros migrados de la versión anterior.</summary>
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public string? Comentario { get; set; }
}
