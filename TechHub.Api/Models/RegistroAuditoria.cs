namespace TechHub.Api.Models;

/// <summary>
/// Bitácora de auditoría: quién hizo qué y cuándo (altas, modificaciones, bajas, cambios de estado, inicios de sesión).
/// Solo la consulta el administrador. Las filas nunca se modifican.
/// </summary>
public class RegistroAuditoria
{
    public long Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    /// <summary>Null cuando no hay sesión (ej: intento de login fallido).</summary>
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    /// <summary>Alta, Modificación, Baja, Cambio de estado, Inicio de sesión, etc.</summary>
    public string Accion { get; set; } = string.Empty;

    /// <summary>Producto, Pedido, Compra, Usuario, Oferta, Combo...</summary>
    public string Entidad { get; set; } = string.Empty;

    /// <summary>Id o código del registro afectado.</summary>
    public string? EntidadId { get; set; }

    /// <summary>Datos enviados en la operación (sin contraseñas).</summary>
    public string? Detalle { get; set; }

    public string? Ip { get; set; }
}
