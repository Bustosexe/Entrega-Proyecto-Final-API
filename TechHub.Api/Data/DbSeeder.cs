using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Models;

namespace TechHub.Api.Data;

/// <summary>
/// Aplica las migraciones pendientes, crea el administrador inicial si no hay usuarios
/// y, si está habilitado, carga los datos de demostración.
/// Se ejecuta al iniciar la aplicación (también en MonsterASP).
/// </summary>
public static class DbSeeder
{
    public static async Task InicializarAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        await db.Database.MigrateAsync();

        if (!await db.Usuarios.AnyAsync())
        {
            var admin = new Usuario
            {
                NombreUsuario = config["Seed:AdminUsuario"] ?? "admin",
                NombreCompleto = "Administrador",
                Email = config["Seed:AdminEmail"] ?? "admin@techhub.local",
                RolId = Roles.AdminId
            };
            admin.PasswordHash = hasher.HashPassword(admin, config["Seed:AdminPassword"] ?? "Admin123!");

            db.Usuarios.Add(admin);
            await db.SaveChangesAsync();
            logger.LogInformation("Usuario administrador inicial creado: {Usuario}", admin.NombreUsuario);
        }

        if (config.GetValue("Seed:DatosDemo", false))
        {
            try
            {
                await DemoSeeder.CargarAsync(db, hasher, logger);
                await DemoSeeder.CargarPromocionesAsync(db, logger);
            }
            catch (Exception ex)
            {
                // Los datos de demo nunca deben impedir que la API arranque.
                logger.LogError(ex, "No se pudieron cargar los datos de demostración.");
            }
        }
    }
}
