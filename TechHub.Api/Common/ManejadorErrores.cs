using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Services;

namespace TechHub.Api.Common;

/// <summary>
/// Convierte las excepciones en respuestas ProblemDetails (RFC 7807) con el código HTTP correcto,
/// sin exponer detalles internos del servidor al cliente.
/// </summary>
public class ManejadorErrores(ILogger<ManejadorErrores> logger, IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception ex, CancellationToken ct)
    {
        var (status, titulo, detalle) = ex switch
        {
            ReglaNegocioException r => (r.StatusCode, "Operación no permitida", r.Message),
            ImagenInvalidaException i => (StatusCodes.Status400BadRequest, "Imagen inválida", i.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "No autorizado", ex.Message),
            DbUpdateException => (StatusCodes.Status409Conflict, "Conflicto de datos",
                "La operación viola una restricción de la base de datos (dato duplicado o registro relacionado)."),
            _ => (StatusCodes.Status500InternalServerError, "Error interno", "Ocurrió un error inesperado.")
        };

        if (status >= 500)
            logger.LogError(ex, "Error no controlado en {Path}", context.Request.Path);
        else
            logger.LogWarning("{Titulo}: {Detalle}", titulo, ex.Message);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = ex,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = titulo,
                Detail = detalle,
                Instance = context.Request.Path
            }
        });
    }
}
