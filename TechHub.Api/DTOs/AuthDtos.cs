using System.ComponentModel.DataAnnotations;

namespace TechHub.Api.DTOs;

public class LoginRequest
{
    [Required(ErrorMessage = "El usuario o email es obligatorio.")]
    [StringLength(150)]
    public string Usuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;
}

public record LoginResponse(
    string Token,
    DateTime Expira,
    UsuarioDto Usuario);

/// <summary>Registro de un comprador desde la app: crea su ficha de Cliente y su cuenta.</summary>
public class RegistroRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El DNI/CUIT es obligatorio.")]
    [RegularExpression(@"^(\d{7,8}|\d{2}-?\d{8}-?\d)$", ErrorMessage = "Ingresá un DNI (7-8 dígitos) o un CUIT válido.")]
    public string Documento { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es obligatorio."), EmailAddress(ErrorMessage = "Email inválido."), StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Telefono { get; set; }

    [StringLength(200)]
    public string? Direccion { get; set; }

    [Required, StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Datos de la cuenta del usuario logueado (y su ficha de cliente si es comprador).</summary>
public record PerfilDto(UsuarioDto Usuario, ClienteDto? Cliente);

public class PerfilRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(150)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(30)]
    public string? Telefono { get; set; }

    [StringLength(200)]
    public string? Direccion { get; set; }

    /// <summary>Opcional: para cambiar la contraseña hay que mandar la actual.</summary>
    [StringLength(100)]
    public string? PasswordActual { get; set; }

    [StringLength(100, MinimumLength = 6, ErrorMessage = "La nueva contraseña debe tener al menos 6 caracteres.")]
    public string? NuevaPassword { get; set; }
}
