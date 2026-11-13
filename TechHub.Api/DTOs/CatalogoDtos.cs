using System.ComponentModel.DataAnnotations;
using TechHub.Api.Common;

namespace TechHub.Api.DTOs;

// ---------------- Categorías ----------------

public record CategoriaDto(int Id, string Nombre, string? Descripcion, bool Activo, int CantidadProductos);

public class CategoriaRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;
}

// ---------------- Productos ----------------

public record ProductoDto(
    int Id,
    string Nombre,
    string Marca,
    string? Descripcion,
    decimal Precio,
    int Stock,
    int StockMinimo,
    bool StockBajo,
    string? ImagenUrl,
    bool Activo,
    int CategoriaId,
    string Categoria);

public class ProductoRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "La marca es obligatoria."), StringLength(80)]
    public string Marca { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Descripcion { get; set; }

    [Range(typeof(decimal), "0", "999999999", ErrorMessage = "El precio debe ser mayor o igual a 0.")]
    public decimal Precio { get; set; }

    [Range(0, 100000)]
    public int StockMinimo { get; set; } = 2;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una categoría.")]
    public int CategoriaId { get; set; }

    public bool Activo { get; set; } = true;
}

/// <summary>Filtros de la búsqueda de productos, además de la paginación.</summary>
public class ProductoQuery : PaginacionQuery
{
    public int? CategoriaId { get; set; }
    public bool? SoloStockBajo { get; set; }
    /// <summary>Si es true, incluye productos dados de baja (solo personal).</summary>
    public bool IncluirInactivos { get; set; }
    /// <summary>nombre | precio | precio_desc | stock</summary>
    public string? Orden { get; set; }
}

// ---------------- Proveedores ----------------

public record ProveedorDto(int Id, string RazonSocial, string Cuit, string? Email, string? Telefono, string? Direccion, bool Activo);

public class ProveedorRequest
{
    [Required(ErrorMessage = "La razón social es obligatoria."), StringLength(150)]
    public string RazonSocial { get; set; } = string.Empty;

    [Required(ErrorMessage = "El CUIT es obligatorio.")]
    [RegularExpression(@"^\d{2}-?\d{8}-?\d$", ErrorMessage = "Formato de CUIT inválido (ej: 30-12345678-9).")]
    public string Cuit { get; set; } = string.Empty;

    [EmailAddress, StringLength(150)]
    public string? Email { get; set; }

    [StringLength(30)]
    public string? Telefono { get; set; }

    [StringLength(200)]
    public string? Direccion { get; set; }

    public bool Activo { get; set; } = true;
}

// ---------------- Clientes ----------------

public record ClienteDto(int Id, string Nombre, string Documento, string? Email, string? Telefono, string? Direccion, bool Activo);

public class ClienteRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El DNI/CUIT es obligatorio.")]
    [RegularExpression(@"^(\d{7,8}|\d{2}-?\d{8}-?\d)$", ErrorMessage = "Ingresá un DNI (7-8 dígitos) o un CUIT válido.")]
    public string Documento { get; set; } = string.Empty;

    [EmailAddress, StringLength(150)]
    public string? Email { get; set; }

    [StringLength(30)]
    public string? Telefono { get; set; }

    [StringLength(200)]
    public string? Direccion { get; set; }

    public bool Activo { get; set; } = true;
}
