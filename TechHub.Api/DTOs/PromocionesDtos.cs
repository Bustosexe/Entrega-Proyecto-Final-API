using System.ComponentModel.DataAnnotations;
using TechHub.Api.Common;

namespace TechHub.Api.DTOs;

// ---------------- Ofertas ----------------

/// <summary>Estado: Vigente, Programada, Vencida o Pausada.</summary>
public record OfertaDto(
    int Id,
    int ProductoId,
    string Producto,
    string? ImagenUrl,
    decimal PrecioLista,
    string? Titulo,
    int Porcentaje,
    decimal PrecioOferta,
    DateOnly Desde,
    DateOnly Hasta,
    bool Activa,
    string Estado);

public class OfertaRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un producto.")]
    public int ProductoId { get; set; }

    [StringLength(80)]
    public string? Titulo { get; set; }

    [Range(1, 90, ErrorMessage = "El descuento debe estar entre 1 y 90 %.")]
    public int Porcentaje { get; set; }

    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public bool Activa { get; set; } = true;
}

public class OfertaQuery : PaginacionQuery
{
    /// <summary>Vigente | Programada | Vencida | Pausada</summary>
    public string? Estado { get; set; }
}

// ---------------- Combos ----------------

public record ComboItemDto(
    int ProductoId,
    string Producto,
    string? ImagenUrl,
    string Categoria,
    int Cantidad,
    decimal PrecioLista,
    decimal PrecioCombo,
    int Stock);

public record ComboDto(
    int Id,
    string Nombre,
    string? Descripcion,
    int Porcentaje,
    bool Activo,
    string? ImagenUrl,
    decimal PrecioLista,
    decimal PrecioCombo,
    decimal Ahorro,
    int StockDisponible,
    bool Disponible,
    IReadOnlyList<ComboItemDto> Items);

public class ComboItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un producto.")]
    public int ProductoId { get; set; }

    [Range(1, 20, ErrorMessage = "La cantidad debe estar entre 1 y 20.")]
    public int Cantidad { get; set; } = 1;
}

public class ComboRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Descripcion { get; set; }

    [Range(1, 50, ErrorMessage = "El descuento debe estar entre 1 y 50 %.")]
    public int Porcentaje { get; set; }

    public bool Activo { get; set; } = true;

    [Required, MinLength(2, ErrorMessage = "Un combo necesita al menos 2 productos."), MaxLength(10)]
    public List<ComboItemRequest> Items { get; set; } = [];
}

public class ComboQuery : PaginacionQuery
{
    /// <summary>Si es true, incluye combos dados de baja (solo personal).</summary>
    public bool IncluirInactivos { get; set; }
}

/// <summary>Par de categorías que pueden ir juntas en un combo.</summary>
public record CompatibilidadDto(int CategoriaAId, string CategoriaA, int CategoriaBId, string CategoriaB);

// ---------------- Auditoría ----------------

public record AuditoriaDto(
    long Id,
    DateTime Fecha,
    string? Usuario,
    string? Rol,
    string Accion,
    string Entidad,
    string? EntidadId,
    string? Detalle,
    string? Ip);

public class AuditoriaQuery : PaginacionQuery
{
    public string? Entidad { get; set; }
    public int? UsuarioId { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
}
