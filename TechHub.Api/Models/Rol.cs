namespace TechHub.Api.Models;

/// <summary>Perfil de acceso (tabla de referencia). Los valores fijos están en <see cref="Roles"/>.</summary>
public class Rol
{
    public int Id { get; set; }
    /// <summary>Nombre que viaja en el JWT y que usa [Authorize(Roles = ...)]: Admin | Comprador.</summary>
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
}
