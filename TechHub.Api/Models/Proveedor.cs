namespace TechHub.Api.Models;

/// <summary>Distribuidor mayorista al que se le compra el hardware.</summary>
public class Proveedor
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string Cuit { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<Ingreso> Ingresos { get; set; } = new List<Ingreso>();
}
