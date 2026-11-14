namespace TechHub.Api.Models;

/// <summary>Valores fijos de la tabla Roles (se cargan con la migración).</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Comprador = "Comprador";
    public const string Todos = Admin + "," + Comprador;

    public const int AdminId = 1;
    public const int CompradorId = 2;
}
