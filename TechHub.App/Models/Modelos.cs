using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Maui.Graphics;
using TechHub.App.Config;

namespace TechHub.App.Models;

/// <summary>Colores usados desde código (badges de estado y stock).</summary>
public static class Paleta
{
    public static readonly Color Primario = Color.FromArgb("#4F46E5");
    public static readonly Color Oscuro = Color.FromArgb("#1E1B4B");
    public static readonly Color Blanco = Colors.White;
    public static readonly Color Exito = Color.FromArgb("#16A34A");
    public static readonly Color Alerta = Color.FromArgb("#D97706");
    public static readonly Color Peligro = Color.FromArgb("#DC2626");
    public static readonly Color Info = Color.FromArgb("#0284C7");
    public static readonly Color Gris = Color.FromArgb("#64748B");
    public static readonly Color FondoChip = Color.FromArgb("#E0E7FF");
    public static readonly Color Violeta = Color.FromArgb("#C026D3");
    public static readonly Color Linea = Color.FromArgb("#E5E7EB");
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

public class ProductoDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
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

    [JsonIgnore] public string ImagenCompleta => ApiConfig.UrlCompleta(ImagenUrl);
    [JsonIgnore] public string PrecioTexto => Formato.Moneda(Precio);
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
    public int SalidaId { get; set; }
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

    [JsonIgnore] public string FechaTexto => Formato.Fecha(Fecha);
    [JsonIgnore] public string TotalTexto => Formato.Moneda(Total);
    [JsonIgnore] public string Resumen => $"{Unidades} {(Unidades == 1 ? "unidad" : "unidades")} · {Items.Count} {(Items.Count == 1 ? "producto" : "productos")}";

    [JsonIgnore] public string ComprobanteFechaTexto => ComprobanteFecha is DateTime f ? Formato.Fecha(f) : string.Empty;

    [JsonIgnore]
    public Color EstadoColor => Estado switch
    {
        "Pendiente" => Paleta.Alerta,
        "EnRevision" => Paleta.Violeta,
        "Confirmado" => Paleta.Info,
        "Entregado" => Paleta.Exito,
        "Cancelado" => Paleta.Peligro,
        _ => Paleta.Gris
    };
}

public class IngresoDto
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public int ProductoId { get; set; }
    public string Producto { get; set; } = string.Empty;
    public int ProveedorId { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal CostoUnitario { get; set; }
    public decimal Total { get; set; }
    public string? NumeroComprobante { get; set; }
    public string? Observaciones { get; set; }
    public string RegistradoPor { get; set; } = string.Empty;

    [JsonIgnore] public string FechaTexto => Formato.Fecha(Fecha);
    [JsonIgnore] public string TotalTexto => Formato.Moneda(Total);
    [JsonIgnore] public string Detalle => $"+{Cantidad} u. x {Formato.Moneda(CostoUnitario)} · {Proveedor}";
    [JsonIgnore] public string Comprobante => string.IsNullOrWhiteSpace(NumeroComprobante) ? $"por {RegistradoPor}" : $"Comp. {NumeroComprobante} · por {RegistradoPor}";
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

public class DatosTransferenciaDto
{
    public string Alias { get; set; } = string.Empty;
    public string Cbu { get; set; } = string.Empty;
    public string Titular { get; set; } = string.Empty;
    public string Cuit { get; set; } = string.Empty;
    public string Banco { get; set; } = string.Empty;
    public int HorasReserva { get; set; }
}

/// <summary>Estados de pedido: código de la API y texto para mostrar.</summary>
public static class EstadosPedido
{
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
