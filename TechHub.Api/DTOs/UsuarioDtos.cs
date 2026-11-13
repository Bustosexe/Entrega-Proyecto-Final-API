using System.ComponentModel.DataAnnotations;
using TechHub.Api.Common;

namespace TechHub.Api.DTOs;

public record UsuarioDto(
    int Id,
    string NombreUsuario,
    string NombreCompleto,
    string Email,
    string Rol,
    bool Activo,
    DateTime FechaCreacion,
    int? ClienteId);

/// <summary>Alta de un administrador (los compradores se registran solos desde la app).</summary>
public class UsuarioCrearRequest
{
    [Required, StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Solo letras, números, punto, guion y guion bajo.")]
    public string NombreUsuario { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    public string Password { get; set; } = string.Empty;
}

public class UsuarioActualizarRequest
{
    [Required, StringLength(150)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    /// <summary>Opcional: si viene, se cambia la contraseña.</summary>
    [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    public string? NuevaPassword { get; set; }
}

public class UsuarioQuery : PaginacionQuery
{
    /// <summary>Admin | Comprador</summary>
    public string? Rol { get; set; }
}
