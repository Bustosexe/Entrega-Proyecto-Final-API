using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TechHub.Api.Common;

/// <summary>Parámetros comunes de paginación y búsqueda (?pagina=1&amp;tamanio=10&amp;buscar=rtx).</summary>
public class PaginacionQuery
{
    public const int TamanioMaximo = 50;

    [Range(1, int.MaxValue)]
    public int Pagina { get; set; } = 1;

    [Range(1, TamanioMaximo)]
    public int Tamanio { get; set; } = 10;

    [StringLength(100)]
    public string? Buscar { get; set; }
}

/// <summary>Respuesta paginada estándar de la API.</summary>
public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Pagina,
    int Tamanio,
    int TotalItems,
    int TotalPaginas);

public static class PaginacionExtensions
{
    /// <summary>
    /// Ejecuta COUNT + SELECT con OFFSET/FETCH en SQL Server: nunca trae a memoria más de una página.
    /// La consulta debe venir ordenada para que la paginación sea estable.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int pagina, int tamanio, CancellationToken ct = default)
    {
        pagina = Math.Max(1, pagina);
        tamanio = Math.Clamp(tamanio, 1, PaginacionQuery.TamanioMaximo);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .ToListAsync(ct);

        var totalPaginas = (int)Math.Ceiling(total / (double)tamanio);
        return new PagedResult<T>(items, pagina, tamanio, total, totalPaginas);
    }
}
