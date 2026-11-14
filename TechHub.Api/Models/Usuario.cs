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
    /// <summary>Perfil de acceso (tabla Roles): Admin o Comprador.</summary>
    public int RolId { get; set; } = Roles.CompradorId;
    public Rol Rol { get; set; } = null!;
    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>Solo para compradores: su ficha de cliente.</summary>
    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    /// <summary>Compras a proveedores que registró (solo administradores).</summary>
    public ICollection<Compra> Compras { get; set; } = new List<Compra>();
    /// <summary>Pedidos que registró (los suyos, o ventas en el local si es administrador).</summary>
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
    /// <summary>Cambios de estado de pedidos que hizo (auditoría).</summary>
    public ICollection<HistorialPedido> CambiosDeEstado { get; set; } = new List<HistorialPedido>();
}
