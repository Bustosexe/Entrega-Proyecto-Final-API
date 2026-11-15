using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TechHub.Api.Common;
using TechHub.Api.Data;
using TechHub.Api.DTOs;
using TechHub.Api.Models;
using TechHub.Api.Services;

namespace TechHub.Api.Controllers;

/// <summary>
/// Pedidos (cabecera) con sus renglones (DetallesPedido) y su historial de estados (HistorialPedidos).
/// Flujo de pago por transferencia: el comprador confirma el carrito (Pendiente, stock reservado),
/// transfiere y sube el comprobante (EnRevision); el admin lo verifica y confirma el pago (Confirmado)
/// o lo rechaza (vuelve a Pendiente); finalmente lo marca como Entregado.
/// </summary>
[ApiController]
[Route("api/pedidos")]
[Authorize(Roles = Roles.Todos)]
[Produces("application/json")]
public class PedidosController(
    AppDbContext db,
    IMovimientoService movimientos,
    IComprobanteService comprobantes,
    INotificacionesService notificaciones) : ControllerBase
{
    /// <summary>
    /// Confirma el carrito. Comprador: compra para sí mismo y el pedido queda Pendiente (esperando la transferencia).
    /// Admin: venta en el local a un cliente (ClienteId obligatorio) y queda Entregado.
    /// Devuelve 409 si algún producto no tiene stock suficiente (no se guarda nada).
    /// </summary>
    [HttpPost]
    [ProducesResponseType<PedidoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PedidoDto>> Crear(PedidoCrearRequest req, CancellationToken ct)
    {
        var usuarioId = User.ObtenerUsuarioId();
        int clienteId;
        string estado;
        var esAdmin = User.IsInRole(Roles.Admin);

        if (esAdmin)
        {
            if (req.ClienteId is not int id)
                return BadRequest(new ProblemDetails { Status = 400, Title = "Cliente requerido", Detail = "Para una venta en el local indicá el cliente." });
            clienteId = id;
            estado = EstadosPedido.Entregado;
        }
        else
        {
            clienteId = await ClienteDelUsuarioAsync(usuarioId, ct);
            estado = EstadosPedido.Pendiente;
        }

        if (req.Items.Count == 0 && req.Combos.Count == 0)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Carrito vacío", Detail = "El carrito está vacío." });

        var codigo = await movimientos.RegistrarPedidoAsync(
            clienteId, usuarioId, req.Items.Select(i => (i.ProductoId, i.Cantidad)), estado, req.Observaciones?.Trim(),
            req.Combos.Select(c => (c.ComboId, c.Cantidad)), ct);

        var pedido = (await db.ObtenerPedidoAsync(codigo, ct))!;
        if (!esAdmin)
        {
            var cliente = await DatosClienteAsync(clienteId, ct);
            notificaciones.PedidoCreado(pedido, cliente.Email);
        }
        return CreatedAtAction(nameof(Obtener), new { codigo }, pedido);
    }

    /// <summary>Historial de compras del comprador logueado.</summary>
    [HttpGet("mios")]
    [Authorize(Roles = Roles.Comprador)]
    public async Task<ActionResult<PagedResult<PedidoDto>>> Mios([FromQuery] PedidoQuery q, CancellationToken ct)
    {
        var clienteId = await ClienteDelUsuarioAsync(User.ObtenerUsuarioId(), ct);
        q.ClienteId = clienteId;
        return await ListarAsync(q, ct);
    }

    /// <summary>Todos los pedidos, con filtro por estado, cliente y búsqueda (código o nombre). Solo Admin.</summary>
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PagedResult<PedidoDto>>> Listar([FromQuery] PedidoQuery q, CancellationToken ct)
        => await ListarAsync(q, ct);

    [HttpGet("{codigo}")]
    public async Task<ActionResult<PedidoDto>> Obtener(string codigo, CancellationToken ct)
    {
        var pedido = await db.ObtenerPedidoAsync(codigo, ct);
        if (pedido is null) return NotFound();
        if (!await PuedeVerAsync(pedido.ClienteId, ct)) return Forbid();
        return Ok(pedido);
    }

    /// <summary>
    /// El comprador sube el comprobante de la transferencia (JPG, PNG, WEBP o PDF, máx. 5 MB).
    /// El pedido pasa a "EnRevision" hasta que el admin lo verifique.
    /// </summary>
    [HttpPost("{codigo}/comprobante")]
    [Authorize(Roles = Roles.Comprador)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<PedidoDto>> SubirComprobante(string codigo, IFormFile archivo, CancellationToken ct)
    {
        var usuarioId = User.ObtenerUsuarioId();
        var clienteId = await ClienteDelUsuarioAsync(usuarioId, ct);
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Codigo == codigo, ct);
        if (pedido is null) return NotFound();
        if (pedido.ClienteId != clienteId) return Forbid();
        if (pedido.EstadoId != EstadosPedido.PendienteId)
            return Conflict(new ProblemDetails { Status = 409, Title = "No se puede subir", Detail = "Solo se puede subir el comprobante de un pedido que espera el pago." });

        var anterior = pedido.ComprobanteArchivo;
        var archivoGuardado = await comprobantes.GuardarAsync(codigo, archivo, ct);
        pedido.ComprobanteArchivo = archivoGuardado;

        // Guarda el archivo y pasa a "Pago en revisión" (queda en el historial).
        await movimientos.CambiarEstadoAsync(codigo, EstadosPedido.EnRevision, usuarioId, null, ct);
        if (anterior != archivoGuardado) comprobantes.Eliminar(anterior);

        return Ok(await db.ObtenerPedidoAsync(codigo, ct));
    }

    /// <summary>Descarga el comprobante de pago. Solo el dueño del pedido o un administrador.</summary>
    [HttpGet("{codigo}/comprobante")]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> VerComprobante(string codigo, CancellationToken ct)
    {
        var info = await db.Pedidos.AsNoTracking()
            .Where(p => p.Codigo == codigo)
            .Select(p => new { p.ClienteId, p.ComprobanteArchivo })
            .FirstOrDefaultAsync(ct);
        if (info is null) return NotFound();
        if (!await PuedeVerAsync(info.ClienteId, ct)) return Forbid();

        var archivo = comprobantes.Obtener(info.ComprobanteArchivo);
        if (archivo is null) return NotFound(new ProblemDetails { Status = 404, Title = "Sin comprobante", Detail = "Este pedido no tiene comprobante cargado." });

        var extension = Path.GetExtension(archivo.Value.Ruta);
        return PhysicalFile(archivo.Value.Ruta, archivo.Value.Mime, $"comprobante-{codigo}{extension}");
    }

    /// <summary>
    /// Admin: Confirmado (pago verificado: requiere comprobante en revisión), Entregado o Cancelado (devuelve el stock).
    /// Al confirmar el pago se le envía al comprador el comprobante del pedido por email.
    /// </summary>
    [HttpPut("{codigo}/estado")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PedidoDto>> CambiarEstado(string codigo, CambiarEstadoRequest req, CancellationToken ct)
    {
        await movimientos.CambiarEstadoAsync(codigo, req.Estado, User.ObtenerUsuarioId(), req.Comentario, ct);
        var pedido = (await db.ObtenerPedidoAsync(codigo, ct))!;

        if (req.Estado == EstadosPedido.Confirmado)
        {
            var cliente = await DatosClienteAsync(pedido.ClienteId, ct);
            notificaciones.PagoConfirmado(pedido, cliente.Email, cliente.Documento);
        }
        return Ok(pedido);
    }

    /// <summary>Admin: rechaza el comprobante (por ejemplo, monto incorrecto). El pedido vuelve a "esperando pago".</summary>
    [HttpPost("{codigo}/rechazar-pago")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PedidoDto>> RechazarPago(string codigo, RechazarPagoRequest req, CancellationToken ct)
    {
        var estadoId = await db.Pedidos.Where(p => p.Codigo == codigo).Select(p => (int?)p.EstadoId).FirstOrDefaultAsync(ct);
        if (estadoId is null) return NotFound();
        if (estadoId != EstadosPedido.EnRevisionId)
            return Conflict(new ProblemDetails { Status = 409, Title = "No se puede rechazar", Detail = "Solo se puede rechazar un pago en revisión." });

        // El motivo queda en el historial y el comprador lo ve en el detalle del pedido.
        await movimientos.CambiarEstadoAsync(codigo, EstadosPedido.Pendiente, User.ObtenerUsuarioId(), req.Motivo, ct);
        return Ok(await db.ObtenerPedidoAsync(codigo, ct));
    }

    /// <summary>El comprador puede cancelar su pedido mientras no esté confirmado. El stock vuelve a estar disponible.</summary>
    [HttpPost("{codigo}/cancelar")]
    [Authorize(Roles = Roles.Comprador)]
    public async Task<ActionResult<PedidoDto>> Cancelar(string codigo, CancellationToken ct)
    {
        var usuarioId = User.ObtenerUsuarioId();
        var clienteId = await ClienteDelUsuarioAsync(usuarioId, ct);
        var info = await db.Pedidos.AsNoTracking()
            .Where(p => p.Codigo == codigo)
            .Select(p => new { p.ClienteId, p.EstadoId })
            .FirstOrDefaultAsync(ct);

        if (info is null) return NotFound();
        if (info.ClienteId != clienteId) return Forbid();
        if (info.EstadoId is not (EstadosPedido.PendienteId or EstadosPedido.EnRevisionId))
            return Conflict(new ProblemDetails { Status = 409, Title = "No se puede cancelar", Detail = "El pago ya fue confirmado. Comunicate con la tienda." });

        await movimientos.CambiarEstadoAsync(codigo, EstadosPedido.Cancelado, usuarioId, "Cancelado por el comprador", ct);
        return Ok(await db.ObtenerPedidoAsync(codigo, ct));
    }

    // ---------------------------------------------------------------------

    private async Task<bool> PuedeVerAsync(int clienteIdPedido, CancellationToken ct)
    {
        if (User.IsInRole(Roles.Admin)) return true;
        var clienteId = await ClienteDelUsuarioAsync(User.ObtenerUsuarioId(), ct);
        return clienteId == clienteIdPedido;
    }

    private async Task<(string? Email, string? Documento)> DatosClienteAsync(int clienteId, CancellationToken ct)
    {
        var c = await db.Clientes.AsNoTracking()
            .Where(x => x.Id == clienteId)
            .Select(x => new { x.Email, x.Documento })
            .FirstOrDefaultAsync(ct);
        return (c?.Email, c?.Documento);
    }

    private async Task<int> ClienteDelUsuarioAsync(int usuarioId, CancellationToken ct)
    {
        var clienteId = await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => u.ClienteId)
            .FirstOrDefaultAsync(ct);

        return clienteId ?? throw new ReglaNegocioException("La cuenta no tiene una ficha de cliente asociada.", StatusCodes.Status403Forbidden);
    }

    /// <summary>Lista pedidos paginados (más recientes primero) con filtros por cliente, estado y búsqueda.</summary>
    private async Task<PagedResult<PedidoDto>> ListarAsync(PedidoQuery q, CancellationToken ct)
    {
        var query = db.Pedidos.AsNoTracking().AsQueryable();

        if (q.ClienteId is int clienteId) query = query.Where(p => p.ClienteId == clienteId);
        if (!string.IsNullOrWhiteSpace(q.Estado)) query = query.Where(p => p.Estado.Codigo == q.Estado);
        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(p => p.Codigo.Contains(b) || p.Cliente.Nombre.Contains(b) || p.Cliente.Documento.Contains(b));
        }

        var ordenados = query.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id);

        var pagina = Math.Max(1, q.Pagina);
        var tamanio = Math.Clamp(q.Tamanio, 1, PaginacionQuery.TamanioMaximo);
        var total = await ordenados.CountAsync(ct);
        var codigosPagina = await ordenados
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .Select(p => p.Codigo)
            .ToListAsync(ct);

        var pedidos = await db.CargarPedidosAsync(codigosPagina, ct);
        var lista = codigosPagina.Where(pedidos.ContainsKey).Select(c => pedidos[c]).ToList();

        return new PagedResult<PedidoDto>(lista, pagina, tamanio, total, (int)Math.Ceiling(total / (double)tamanio));
    }
}
