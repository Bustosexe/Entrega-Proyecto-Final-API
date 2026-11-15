using System.ComponentModel.DataAnnotations;
using TechHub.Api.Common;

namespace TechHub.Api.DTOs;

// ---------------- Compras a proveedores (cabecera + detalle) ----------------

public record DetalleCompraDto(
    int Id,
    int ProductoId,
    string Producto,
    string? ImagenUrl,
    int Cantidad,
    decimal CostoUnitario,
    decimal Subtotal);

public record CompraDto(
    int Id,
    DateTime Fecha,
    int ProveedorId,
    string Proveedor,
    string? NumeroComprobante,
    string? Observaciones,
    string RegistradoPor,
    int Unidades,
    decimal Total,
    IReadOnlyList<DetalleCompraDto> Items);

public class DetalleCompraRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un producto.")]
    public int ProductoId { get; set; }

    [Range(1, 100000, ErrorMessage = "La cantidad debe ser mayor a 0.")]
    public int Cantidad { get; set; }

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "El costo debe ser mayor o igual a 0.")]
    public decimal CostoUnitario { get; set; }
}

public class CompraRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un proveedor.")]
    public int ProveedorId { get; set; }

    [StringLength(50)]
    public string? NumeroComprobante { get; set; }

    [StringLength(500)]
    public string? Observaciones { get; set; }

    [Required, MinLength(1, ErrorMessage = "Agregá al menos un producto."), MaxLength(50)]
    public List<DetalleCompraRequest> Items { get; set; } = [];
}

/// <summary>Filtros del listado de compras.</summary>
public class CompraQuery : PaginacionQuery
{
    public int? ProveedorId { get; set; }
    public int? ProductoId { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}

// ---------------- Pedidos (cabecera + detalle + historial) ----------------

public record PedidoItemDto(
    int DetalleId,
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
    string EstadoTexto,
    int ClienteId,
    string Cliente,
    string? ClienteTelefono,
    string? ClienteDireccion,
    decimal Total,
    int Unidades,
    string? Observaciones,
    bool TieneComprobante,
    DateTime? ComprobanteFecha,
    string? MotivoRechazo,
    IReadOnlyList<PedidoItemDto> Items,
    IReadOnlyList<HistorialPedidoDto> Historial);

/// <summary>Un cambio de estado del pedido: cuándo, a qué estado, quién y por qué.</summary>
public record HistorialPedidoDto(
    DateTime Fecha,
    string Estado,
    string EstadoTexto,
    string? Usuario,
    bool PorAdministrador,
    string? Comentario);

public class RechazarPagoRequest
{
    [Required(ErrorMessage = "Indicá el motivo del rechazo."), StringLength(300)]
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Datos de la cuenta para transferir (se muestran en el carrito y en el pedido).</summary>
public record DatosTransferenciaDto(string Alias, string Cbu, string Titular, string Cuit, string Banco, int HorasReserva);

/// <summary>Envío de un reporte por email. Tipo: pedido | inventario | ventas.</summary>
public class EnviarReporteRequest
{
    [Required, RegularExpression("^(pedido|inventario|ventas)$", ErrorMessage = "Tipo inválido: pedido, inventario o ventas.")]
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Destinatario. Si se omite, se usa el email del usuario logueado.</summary>
    [EmailAddress(ErrorMessage = "Email inválido."), StringLength(150)]
    public string? Email { get; set; }

    public string? Codigo { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}

public class PedidoItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Producto inválido.")]
    public int ProductoId { get; set; }

    [Range(1, 100, ErrorMessage = "La cantidad debe estar entre 1 y 100.")]
    public int Cantidad { get; set; }
}

public class PedidoComboRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Combo inválido.")]
    public int ComboId { get; set; }

    [Range(1, 10, ErrorMessage = "La cantidad de combos debe estar entre 1 y 10.")]
    public int Cantidad { get; set; } = 1;
}

public class PedidoCrearRequest
{
    /// <summary>Productos sueltos. Puede ir vacío si el pedido tiene combos (al menos uno de los dos es obligatorio).</summary>
    [MaxLength(30)]
    public List<PedidoItemRequest> Items { get; set; } = [];

    /// <summary>Combos del carrito: se cobran con el descuento del combo.</summary>
    [MaxLength(10)]
    public List<PedidoComboRequest> Combos { get; set; } = [];

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

    /// <summary>Opcional: queda registrado en el historial del pedido (ej: motivo de la cancelación).</summary>
    [StringLength(300)]
    public string? Comentario { get; set; }
}

public class PedidoQuery : PaginacionQuery
{
    /// <summary>Pendiente | EnRevision | Confirmado | Entregado | Cancelado</summary>
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
    int PagosEnRevision,
    int VentasHoy,
    decimal FacturacionHoy,
    decimal FacturacionMes,
    int Clientes,
    int Proveedores);

/// <summary>Ventas de un día (hora de Argentina) para el gráfico del panel.</summary>
public record VentaDiaDto(DateTime Fecha, string Etiqueta, decimal Total, int Pedidos);

/// <summary>Ranking de productos por unidades vendidas.</summary>
public record TopProductoDto(int ProductoId, string Producto, string? ImagenUrl, int Unidades, decimal Total);

/// <summary>Ventas cobradas agrupadas por marca.</summary>
public record VentaMarcaDto(int MarcaId, string Marca, int Unidades, decimal Total);
