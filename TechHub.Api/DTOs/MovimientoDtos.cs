using System.ComponentModel.DataAnnotations;
using TechHub.Api.Common;

namespace TechHub.Api.DTOs;

// ---------------- Ingresos (compras a proveedor) ----------------

public record IngresoDto(
    int Id,
    DateTime Fecha,
    int ProductoId,
    string Producto,
    int ProveedorId,
    string Proveedor,
    int Cantidad,
    decimal CostoUnitario,
    decimal Total,
    string? NumeroComprobante,
    string? Observaciones,
    string RegistradoPor);

public class IngresoRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un producto.")]
    public int ProductoId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un proveedor.")]
    public int ProveedorId { get; set; }

    [Range(1, 100000, ErrorMessage = "La cantidad debe ser mayor a 0.")]
    public int Cantidad { get; set; }

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "El costo debe ser mayor o igual a 0.")]
    public decimal CostoUnitario { get; set; }

    [StringLength(50)]
    public string? NumeroComprobante { get; set; }

    [StringLength(500)]
    public string? Observaciones { get; set; }
}

/// <summary>Filtros para el historial de ingresos.</summary>
public class MovimientoQuery : PaginacionQuery
{
    public int? ProductoId { get; set; }
    /// <summary>Proveedor en ingresos.</summary>
    public int? TerceroId { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}

// ---------------- Pedidos (Salidas agrupadas por CodigoPedido) ----------------

public record PedidoItemDto(
    int SalidaId,
    int ProductoId,
    string Producto,
    string? ImagenUrl,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Subtotal);

public record PedidoDto(
    string Codigo,
    DateTime Fecha,
    string Estado,
    int ClienteId,
    string Cliente,
    string? ClienteTelefono,
    string? ClienteDireccion,
    decimal Total,
    int Unidades,
    string? Observaciones,
    IReadOnlyList<PedidoItemDto> Items);

public class PedidoItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Producto inválido.")]
    public int ProductoId { get; set; }

    [Range(1, 100, ErrorMessage = "La cantidad debe estar entre 1 y 100.")]
    public int Cantidad { get; set; }
}

public class PedidoCrearRequest
{
    [Required, MinLength(1, ErrorMessage = "El carrito está vacío."), MaxLength(30)]
    public List<PedidoItemRequest> Items { get; set; } = [];

    [StringLength(500)]
    public string? Observaciones { get; set; }

    /// <summary>Solo Admin (venta en el local): cliente al que se le vende. El comprador siempre compra para sí mismo.</summary>
    public int? ClienteId { get; set; }
}

public class CambiarEstadoRequest
{
    [Required]
    [RegularExpression("^(Confirmado|Entregado|Cancelado)$", ErrorMessage = "Estado inválido: Confirmado, Entregado o Cancelado.")]
    public string Estado { get; set; } = string.Empty;
}

public class PedidoQuery : PaginacionQuery
{
    /// <summary>Pendiente | Confirmado | Entregado | Cancelado</summary>
    public string? Estado { get; set; }
    public int? ClienteId { get; set; }
}

// ---------------- Dashboard ----------------

public record ResumenDto(
    int TotalProductos,
    int ProductosStockBajo,
    int UnidadesEnStock,
    decimal ValorInventario,
    int PedidosPendientes,
    int VentasHoy,
    decimal FacturacionHoy,
    decimal FacturacionMes,
    int Clientes,
    int Proveedores);
