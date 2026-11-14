namespace TechHub.Api.Models;

/// <summary>Renglón del pedido: un producto, su cantidad y el precio congelado al momento de la compra.</summary>
public class DetallePedido
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;

    public int ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;

    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
}
