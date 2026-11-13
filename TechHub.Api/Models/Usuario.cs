namespace TechHub.Api.Models;

/// <summary>
/// Cuenta de acceso al sistema. Rol Admin = personal de la tienda.
/// Rol Comprador = cliente registrado desde la app (vinculado 1 a 1 con su ficha de Cliente).
/// </summary>
public class Usuario
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Rol { get; set; } = Roles.Comprador;
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>Solo para compradores: su ficha de cliente.</summary>
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public ICollection<Ingreso> Ingresos { get; set; } = new List<Ingreso>();
    public ICollection<Salida> Salidas { get; set; } = new List<Salida>();
}
