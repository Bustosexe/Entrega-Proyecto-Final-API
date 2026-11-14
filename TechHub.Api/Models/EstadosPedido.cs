namespace TechHub.Api.Models;

/// <summary>
/// Ciclo de vida de un pedido (todas las Salidas que comparten CodigoPedido).
/// Pendiente (esperando la transferencia) → EnRevision (el comprador subió el comprobante)
/// → Confirmado (el admin verificó el pago) → Entregado.
/// </summary>
public static class EstadosPedido
{
    public const string Pendiente = "Pendiente";   // stock reservado, esperando la transferencia
    public const string EnRevision = "EnRevision"; // comprobante subido, el admin lo tiene que verificar
    public const string Confirmado = "Confirmado"; // pago verificado
    public const string Entregado = "Entregado";   // retirado o enviado: venta cerrada
    public const string Cancelado = "Cancelado";   // stock devuelto

    public static readonly string[] Todos = [Pendiente, EnRevision, Confirmado, Entregado, Cancelado];

    /// <summary>Estados que cuentan como venta cobrada (facturación).</summary>
    public static readonly string[] Cobrados = [Confirmado, Entregado];

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
