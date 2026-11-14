namespace TechHub.Api.Models;

/// <summary>Persona o empresa que compra en la tienda (desde la app o en el local).</summary>
public class Cliente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    /// <summary>DNI para personas o CUIT para empresas.</summary>
    public string Documento { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public bool Activo { get; set; } = true;

    /// <summary>Cuenta de la app, si el cliente se registró.</summary>
    public Usuario? Usuario { get; set; }

    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
