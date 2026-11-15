namespace TechHub.Api.Services;

/// <summary>Cálculo de precios con descuento (ofertas y combos). Siempre redondea a 2 decimales, como la base de datos.</summary>
public static class Precios
{
    /// <summary>Argentina no usa horario de verano: todo el año es UTC-3.</summary>
    public const int HorasArgentina = -3;

    public static decimal ConDescuento(decimal precio, int porcentaje) =>
        Math.Round(precio * (100 - porcentaje) / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Primer instante del día (hora de Argentina) expresado en UTC.</summary>
    public static DateTime InicioDiaUtc(DateOnly dia) =>
        DateTime.SpecifyKind(dia.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc).AddHours(-HorasArgentina);

    /// <summary>Último segundo del día (hora de Argentina) expresado en UTC.</summary>
    public static DateTime FinDiaUtc(DateOnly dia) => InicioDiaUtc(dia).AddDays(1).AddSeconds(-1);

    /// <summary>Convierte un instante UTC a la fecha en Argentina.</summary>
    public static DateOnly FechaArgentina(DateTime utc) => DateOnly.FromDateTime(utc.AddHours(HorasArgentina));
}
