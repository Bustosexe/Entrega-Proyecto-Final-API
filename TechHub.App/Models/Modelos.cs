using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Maui.Graphics;
using TechHub.App.Config;

namespace TechHub.App.Models;

/// <summary>Colores usados desde código (badges de estado y stock).</summary>
public static class Paleta
{
    public static readonly Color Primario = Color.FromArgb("#D63224");
    public static readonly Color Oscuro = Color.FromArgb("#1C1C1F");
    public static readonly Color Blanco = Colors.White;
    public static readonly Color Exito = Color.FromArgb("#16A34A");
    public static readonly Color Alerta = Color.FromArgb("#D97706");
    public static readonly Color Peligro = Color.FromArgb("#B91C1C");
    public static readonly Color Info = Color.FromArgb("#0284C7");
    public static readonly Color Gris = Color.FromArgb("#6B6B73");
    public static readonly Color FondoChip = Color.FromArgb("#E4E4E7");
    public static readonly Color Violeta = Color.FromArgb("#7C3AED");
    public static readonly Color Linea = Color.FromArgb("#E4E4E7");
}

public static class Formato
{
    public static readonly CultureInfo Cultura = new("es-AR");
    public static string Moneda(decimal valor) => valor.ToString("C", Cultura);
    public static string Fecha(DateTime fechaUtc) =>
        DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm", Cultura);
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int Pagina { get; set; }
    public int Tamanio { get; set; }
    public int TotalItems { get; set; }
    public int TotalPaginas { get; set; }
}

// ---------------- Sesión ----------------

public class UsuarioDto
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
    public int? ClienteId { get; set; }

    [JsonIgnore] public bool EsAdmin => Rol == "Admin";
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expira { get; set; }
    public UsuarioDto Usuario { get; set; } = new();
}

public class ClienteDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public bool Activo { get; set; }
}

public class PerfilDto
{
    public UsuarioDto Usuario { get; set; } = new();
    public ClienteDto? Cliente { get; set; }
}

// ---------------- Catálogo ----------------

public class CategoriaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
    public int CantidadProductos { get; set; }

    public override string ToString() => Nombre; // texto que muestra el Picker
}

public class MarcaDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public int CantidadProductos { get; set; }

    public override string ToString() => Nombre; // texto que muestra el Picker
}

public class ProductoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int MarcaId { get; set; }
    public string Marca { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public int StockMinimo { get; set; }
    public bool StockBajo { get; set; }
    public string? ImagenUrl { get; set; }
    public bool Activo { get; set; }
    public int CategoriaId { get; set; }
    public string Categoria { get; set; } = string.Empty;
    /// <summary>Descuento de la oferta vigente (null si no hay).</summary>
    public int? DescuentoPct { get; set; }
    /// <summary>Precio que se cobra hoy (precio de lista menos la oferta vigente).</summary>
    public decimal PrecioFinal { get; set; }

    [JsonIgnore] public string ImagenCompleta => ApiConfig.UrlCompleta(ImagenUrl);
    [JsonIgnore] public bool TieneOferta => DescuentoPct is > 0;
    [JsonIgnore] public decimal PrecioVigente => TieneOferta ? PrecioFinal : Precio;
    [JsonIgnore] public string PrecioTexto => Formato.Moneda(PrecioVigente);
    [JsonIgnore] public string PrecioListaTexto => Formato.Moneda(Precio);
    [JsonIgnore] public string DescuentoTexto => $"-{DescuentoPct}%";
    [JsonIgnore] public bool HayStock => Stock > 0;
    [JsonIgnore] public string MarcaCategoria => $"{Marca} · {Categoria}";

    [JsonIgnore]
    public string StockTexto => Stock <= 0 ? "Sin stock" : StockBajo ? $"¡Quedan {Stock}!" : $"{Stock} disponibles";

    [JsonIgnore]
    public Color StockColor => Stock <= 0 ? Paleta.Peligro : StockBajo ? Paleta.Alerta : Paleta.Exito;

    [JsonIgnore] public string EstadoTexto => Activo ? "" : "INACTIVO";
    [JsonIgnore] public bool PuedeAgregar => Activo && Stock > 0;

    public override string ToString() => $"{Nombre} (stock {Stock})"; // texto del Picker de compras
}

public class ProveedorDto
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string Cuit { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public bool Activo { get; set; }

    public override string ToString() => $"{RazonSocial} ({Cuit})";
}

// ---------------- Pedidos ----------------

public class PedidoItemDto
{
    public int DetalleId { get; set; }
    public int ProductoId { get; set; }
    public string Producto { get; set; } = string.Empty;
    public string? ImagenUrl { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal { get; set; }

    [JsonIgnore] public string ImagenCompleta => ApiConfig.UrlCompleta(ImagenUrl);
    [JsonIgnore] public string Detalle => $"{Cantidad} x {Formato.Moneda(PrecioUnitario)}";
    [JsonIgnore] public string SubtotalTexto => Formato.Moneda(Subtotal);
}

public class PedidoDto
{
    public string Codigo { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Estado { get; set; } = string.Empty;
    /// <summary>Texto para mostrar: "Esperando pago", "Pago en revisión", etc.</summary>
    public string EstadoTexto { get; set; } = string.Empty;
    public bool TieneComprobante { get; set; }
    public DateTime? ComprobanteFecha { get; set; }
    public string? MotivoRechazo { get; set; }
    public int ClienteId { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public string? ClienteTelefono { get; set; }
    public string? ClienteDireccion { get; set; }
    public decimal Total { get; set; }
    public int Unidades { get; set; }
    public string? Observaciones { get; set; }
    public List<PedidoItemDto> Items { get; set; } = [];
    /// <summary>Cada cambio de estado (tabla HistorialPedidos): fecha, quién y comentario.</summary>
    public List<HistorialPedidoDto> Historial { get; set; } = [];

    [JsonIgnore] public string FechaTexto => Formato.Fecha(Fecha);
    [JsonIgnore] public string TotalTexto => Formato.Moneda(Total);
    [JsonIgnore] public string Resumen => $"{Unidades} {(Unidades == 1 ? "unidad" : "unidades")} · {Items.Count} {(Items.Count == 1 ? "producto" : "productos")}";

    [JsonIgnore] public string ComprobanteFechaTexto => ComprobanteFecha is DateTime f ? Formato.Fecha(f) : string.Empty;

    [JsonIgnore] public Color EstadoColor => EstadosPedido.ColorDe(Estado);
}

public class HistorialPedidoDto
{
    public DateTime Fecha { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string EstadoTexto { get; set; } = string.Empty;
    public string? Usuario { get; set; }
    public bool PorAdministrador { get; set; }
    public string? Comentario { get; set; }

    /// <summary>"por vos", "por la tienda", "por Ana (admin)"... lo completa la pantalla según quién mira.</summary>
    [JsonIgnore] public string Quien { get; set; } = string.Empty;

    [JsonIgnore] public string FechaTexto => Formato.Fecha(Fecha);
    [JsonIgnore] public string FechaCorta =>
        DateTime.SpecifyKind(Fecha, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM HH:mm", Formato.Cultura);
    [JsonIgnore] public bool TieneComentario => !string.IsNullOrWhiteSpace(Comentario);
    [JsonIgnore] public string Detalle => string.IsNullOrEmpty(Quien) ? FechaTexto : $"{FechaTexto} · {Quien}";
    [JsonIgnore] public Color Color => EstadosPedido.ColorDe(Estado);
}

// ---------------- Compras a proveedores ----------------

public class DetalleCompraDto
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public string Producto { get; set; } = string.Empty;
    public string? ImagenUrl { get; set; }
    public int Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Subtotal { get; set; }

    [JsonIgnore] public string Linea => $"{Cantidad} × {Producto}";
    [JsonIgnore] public string SubtotalTexto => Formato.Moneda(Subtotal);
}

public class CompraDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int ProveedorId { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public string? NumeroComprobante { get; set; }
    public string? Observaciones { get; set; }
    public string RegistradoPor { get; set; } = string.Empty;
    public int Unidades { get; set; }
    public decimal Total { get; set; }
    public List<DetalleCompraDto> Items { get; set; } = [];

    [JsonIgnore] public string FechaTexto => Formato.Fecha(Fecha);
    [JsonIgnore] public string TotalTexto => Formato.Moneda(Total);
    [JsonIgnore] public string Factura => string.IsNullOrWhiteSpace(NumeroComprobante) ? "Sin número de factura" : $"Factura {NumeroComprobante}";
    [JsonIgnore] public string Resumen =>
        $"{Items.Count} {(Items.Count == 1 ? "producto" : "productos")} · {Unidades} {(Unidades == 1 ? "unidad" : "unidades")}";
    [JsonIgnore] public string Registro => $"{FechaTexto} · por {RegistradoPor}";
}

public class ResumenDto
{
    public int TotalProductos { get; set; }
    public int ProductosStockBajo { get; set; }
    public int UnidadesEnStock { get; set; }
    public decimal ValorInventario { get; set; }
    public int PedidosPendientes { get; set; }
    public int PagosEnRevision { get; set; }
    public int VentasHoy { get; set; }
    public decimal FacturacionHoy { get; set; }
    public decimal FacturacionMes { get; set; }
    public int Clientes { get; set; }
    public int Proveedores { get; set; }
}

public class VentaDiaDto
{
    public DateTime Fecha { get; set; }
    public string Etiqueta { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Pedidos { get; set; }
}

public class TopProductoDto
{
    public int ProductoId { get; set; }
    public string Producto { get; set; } = string.Empty;
    public string? ImagenUrl { get; set; }
    public int Unidades { get; set; }
    public decimal Total { get; set; }

    /// <summary>Proporción respecto del más vendido (0 a 1), para la barra de progreso.</summary>
    [JsonIgnore] public double Proporcion { get; set; }
    [JsonIgnore] public int Puesto { get; set; }
    [JsonIgnore] public string PuestoTexto => $"#{Puesto}";
    [JsonIgnore] public string ImagenCompleta => ApiConfig.UrlCompleta(ImagenUrl);
    [JsonIgnore] public string Detalle => $"{Unidades} {(Unidades == 1 ? "unidad" : "unidades")} · {Formato.Moneda(Total)}";
}

public class VentaMarcaDto
{
    public int MarcaId { get; set; }
    public string Marca { get; set; } = string.Empty;
    public int Unidades { get; set; }
    public decimal Total { get; set; }

    /// <summary>Proporción respecto de la marca que más facturó (0 a 1), para la barra.</summary>
    [JsonIgnore] public double Proporcion { get; set; }
    [JsonIgnore] public string TotalTexto => Formato.Moneda(Total);
    [JsonIgnore] public string Detalle => $"{Unidades} {(Unidades == 1 ? "unidad" : "unidades")}";
}

public class DatosTransferenciaDto
{
    public string Alias { get; set; } = string.Empty;
    public string Cbu { get; set; } = string.Empty;
    public string Titular { get; set; } = string.Empty;
    public string Cuit { get; set; } = string.Empty;
    public string Banco { get; set; } = string.Empty;
    public int HorasReserva { get; set; }
}

/// <summary>Estados de pedido: código de la API, texto para mostrar y color.</summary>
public static class EstadosPedido
{
    public static Color ColorDe(string estado) => estado switch
    {
        "Pendiente" => Paleta.Alerta,
        "EnRevision" => Paleta.Violeta,
        "Confirmado" => Paleta.Info,
        "Entregado" => Paleta.Exito,
        "Cancelado" => Paleta.Peligro,
        _ => Paleta.Gris
    };

    public static readonly (string Codigo, string Texto)[] Filtros =
    [
        ("", "Todos"),
        ("Pendiente", "Esperando pago"),
        ("EnRevision", "Pago en revisión"),
        ("Confirmado", "Pago confirmado"),
        ("Entregado", "Entregado"),
        ("Cancelado", "Cancelado")
    ];
}

// ---------------- Ofertas, combos y auditoría ----------------

public class OfertaDto
{
    public int Id { get; set; }
    public int ProductoId { get; set; }
    public string Producto { get; set; } = string.Empty;
    public string? ImagenUrl { get; set; }
    public decimal PrecioLista { get; set; }
    public string? Titulo { get; set; }
    public int Porcentaje { get; set; }
    public decimal PrecioOferta { get; set; }
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public bool Activa { get; set; }
    /// <summary>Vigente, Programada, Vencida o Pausada.</summary>
    public string Estado { get; set; } = string.Empty;
}

public class ComboItemDto
{
    public int ProductoId { get; set; }
    public string Producto { get; set; } = string.Empty;
    public string? ImagenUrl { get; set; }
    public string Categoria { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal PrecioLista { get; set; }
    public decimal PrecioCombo { get; set; }
    public int Stock { get; set; }

    [JsonIgnore] public string ImagenCompleta => ApiConfig.UrlCompleta(ImagenUrl);
    [JsonIgnore] public string Titulo => Cantidad > 1 ? $"{Cantidad} × {Producto}" : Producto;
    [JsonIgnore] public string PrecioTexto => Formato.Moneda(PrecioCombo * Cantidad);
    [JsonIgnore] public string PrecioListaTexto => Formato.Moneda(PrecioLista * Cantidad);
}

public class ComboDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Porcentaje { get; set; }
    public bool Activo { get; set; }
    public string? ImagenUrl { get; set; }
    public decimal PrecioLista { get; set; }
    public decimal PrecioCombo { get; set; }
    public decimal Ahorro { get; set; }
    public int StockDisponible { get; set; }
    public bool Disponible { get; set; }
    public List<ComboItemDto> Items { get; set; } = [];

    [JsonIgnore] public string ImagenCompleta => ApiConfig.UrlCompleta(ImagenUrl);
    [JsonIgnore] public string PrecioTexto => Formato.Moneda(PrecioCombo);
    [JsonIgnore] public string PrecioListaTexto => Formato.Moneda(PrecioLista);
    [JsonIgnore] public string AhorroTexto => $"Ahorrás {Formato.Moneda(Ahorro)}";
    [JsonIgnore] public string DescuentoTexto => $"-{Porcentaje}%";
    [JsonIgnore] public string ResumenItems => string.Join(" + ", Items.Select(i => i.Titulo));
    [JsonIgnore] public string CantidadProductosTexto => Items.Sum(i => i.Cantidad) == 1 ? "1 producto" : $"{Items.Sum(i => i.Cantidad)} productos";
    [JsonIgnore] public string StockTexto => !Disponible ? "Sin stock" : StockDisponible <= 3 ? $"¡Quedan {StockDisponible}!" : $"{StockDisponible} disponibles";
    [JsonIgnore] public Color StockColor => !Disponible ? Paleta.Peligro : StockDisponible <= 3 ? Paleta.Alerta : Paleta.Exito;
}

/// <summary>Par de categorías que pueden ir juntas en un combo.</summary>
public class CompatibilidadDto
{
    public int CategoriaAId { get; set; }
    public string CategoriaA { get; set; } = string.Empty;
    public int CategoriaBId { get; set; }
    public string CategoriaB { get; set; } = string.Empty;
}
