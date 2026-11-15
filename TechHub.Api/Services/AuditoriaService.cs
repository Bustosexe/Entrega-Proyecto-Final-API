using Microsoft.EntityFrameworkCore;
using TechHub.Api.Data;
using TechHub.Api.Models;

namespace TechHub.Api.Services;

public interface IAuditoriaService
{
    /// <summary>Guarda una fila en la bitácora. Nunca lanza excepciones: la auditoría no debe romper la operación.</summary>
    Task RegistrarAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? detalle, string? ip);
}

public class AuditoriaService(AppDbContext db, ILogger<AuditoriaService> logger) : IAuditoriaService
{
    public async Task RegistrarAsync(int? usuarioId, string accion, string entidad, string? entidadId, string? detalle, string? ip)
    {
        var registro = new RegistroAuditoria
        {
            Fecha = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Accion = Cortar(accion, 60)!,
            Entidad = Cortar(entidad, 40)!,
            EntidadId = Cortar(entidadId, 50),
            Detalle = Cortar(detalle, 1000),
            Ip = Cortar(ip, 45)
        };

        try
        {
            db.Auditoria.Add(registro);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo registrar la auditoría ({Accion} {Entidad}).", accion, entidad);
            // Evita que la fila fallida quede pendiente en el contexto y se reintente en otro guardado.
            db.Entry(registro).State = EntityState.Detached;
        }
    }

    private static string? Cortar(string? texto, int max) =>
        texto is null ? null : texto.Length <= max ? texto : texto[..(max - 1)] + "…";
}
