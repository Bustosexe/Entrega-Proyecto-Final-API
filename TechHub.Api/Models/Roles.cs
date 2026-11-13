namespace TechHub.Api.Models;

public static class Roles
{
    /// <summary>Personal de la tienda: gestiona todo el sistema.</summary>
    public const string Admin = "Admin";
    /// <summary>Cliente registrado desde la app: compra y ve sus pedidos.</summary>
    public const string Comprador = "Comprador";
    public const string Todos = Admin + "," + Comprador;
}
