namespace TechHub.Api.Models;

/// <summary>
/// Valores fijos de la tabla EstadosPedido y reglas del ciclo de vida de un pedido:
/// Pendiente → EnRevision → Confirmado → Entregado (o Cancelado, que devuelve el stock).
/// </summary>
public static class EstadosPedido
{
    // Códigos (los que viajan en la API)
    public const string Pendiente = "Pendiente";   // stock reservado, esperando la transferencia
    public const string EnRevision = "EnRevision"; // comprobante subido, el admin lo tiene que verificar
    public const string Confirmado = "Confirmado"; // pago verificado
    public const string Entregado = "Entregado";   // retirado o enviado: venta cerrada
    public const string Cancelado = "Cancelado";   // stock devuelto

    // Ids de la tabla EstadosPedido
    public const int PendienteId = 1;
    public const int EnRevisionId = 2;
    public const int ConfirmadoId = 3;
    public const int EntregadoId = 4;
    public const int CanceladoId = 5;

    public static readonly string[] Todos = [Pendiente, EnRevision, Confirmado, Entregado, Cancelado];

    /// <summary>Estados que cuentan como venta cobrada (facturación, ranking, reportes).</summary>
    public static readonly int[] CobradosIds = [ConfirmadoId, EntregadoId];

    public static int Id(string codigo) => codigo switch
    {
        Pendiente => PendienteId,
        EnRevision => EnRevisionId,
        Confirmado => ConfirmadoId,
        Entregado => EntregadoId,
        Cancelado => CanceladoId,
        _ => throw new ArgumentException($"Estado desconocido: {codigo}", nameof(codigo))
    };

    public static string Codigo(int id) => id switch
    {
        PendienteId => Pendiente,
        EnRevisionId => EnRevision,
        ConfirmadoId => Confirmado,
        EntregadoId => Entregado,
        CanceladoId => Cancelado,
        _ => throw new ArgumentException($"Estado desconocido: {id}", nameof(id))
    };

    public static string Texto(string estado) => estado switch
    {
        Pendiente => "Esperando pago",
        EnRevision => "Pago en revisión",
        Confirmado => "Pago confirmado",
        Entregado => "Entregado",
        Cancelado => "Cancelado",
        _ => estado
    };

    public static bool TransicionValida(string actual, string nuevo) => (actual, nuevo) switch
    {
        (Pendiente, EnRevision) => true,   // el comprador sube el comprobante
        (Pendiente, Cancelado) => true,
        (EnRevision, Confirmado) => true,  // el admin aprueba el pago
        (EnRevision, Pendiente) => true,   // el admin rechaza el comprobante
        (EnRevision, Cancelado) => true,
        (Confirmado, Entregado) => true,
        (Confirmado, Cancelado) => true,
        _ => false
    };
}
