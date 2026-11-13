namespace TechHub.Api.Models;

/// <summary>Ciclo de vida de un pedido (todas las Salidas que comparten CodigoPedido).</summary>
public static class EstadosPedido
{
    public const string Pendiente = "Pendiente";   // stock reservado, esperando confirmación
    public const string Confirmado = "Confirmado"; // pago/confirmación recibida
    public const string Entregado = "Entregado";   // retirado o enviado: venta cerrada
    public const string Cancelado = "Cancelado";   // stock devuelto

    public static readonly string[] Todos = [Pendiente, Confirmado, Entregado, Cancelado];

    public static bool TransicionValida(string actual, string nuevo) => (actual, nuevo) switch
    {
        (Pendiente, Confirmado) => true,
        (Pendiente, Entregado) => true,
        (Pendiente, Cancelado) => true,
        (Confirmado, Entregado) => true,
        (Confirmado, Cancelado) => true,
        _ => false
    };
}
