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
/// Pedidos = Salidas agrupadas por CodigoPedido.
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

        var codigo = await movimientos.RegistrarPedidoAsync(
            clienteId, usuarioId, req.Items.Select(i => (i.ProductoId, i.Cantidad)), estado, req.Observaciones?.Trim(), ct);

        var pedido = (await ObtenerPedidoAsync(codigo, ct))!;
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
        var pedido = await ObtenerPedidoAsync(codigo, ct);
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
        var clienteId = await ClienteDelUsuarioAsync(User.ObtenerUsuarioId(), ct);
        var filas = await db.Salidas.Where(s => s.CodigoPedido == codigo).ToListAsync(ct);
        if (filas.Count == 0) return NotFound();
        if (filas[0].ClienteId != clienteId) return Forbid();
        if (filas[0].Estado != EstadosPedido.Pendiente)
            return Conflict(new ProblemDetails { Status = 409, Title = "No se puede subir", Detail = "Solo se puede subir el comprobante de un pedido que espera el pago." });

        var anterior = filas[0].ComprobanteArchivo;
        var archivoGuardado = await comprobantes.GuardarAsync(codigo, archivo, ct);
        foreach (var f in filas)
        {
            f.ComprobanteArchivo = archivoGuardado;
            f.ComprobanteFecha = DateTime.UtcNow;
            f.MotivoRechazo = null;
            f.Estado = EstadosPedido.EnRevision;
        }
        await db.SaveChangesAsync(ct);
        if (anterior != archivoGuardado) comprobantes.Eliminar(anterior);

        return Ok(await ObtenerPedidoAsync(codigo, ct));
    }

    /// <summary>Descarga el comprobante de pago. Solo el dueño del pedido o un administrador.</summary>
    [HttpGet("{codigo}/comprobante")]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> VerComprobante(string codigo, CancellationToken ct)
    {
        var info = await db.Salidas.AsNoTracking()
            .Where(s => s.CodigoPedido == codigo)
            .Select(s => new { s.ClienteId, s.ComprobanteArchivo })
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
        await movimientos.CambiarEstadoAsync(codigo, req.Estado, ct);
        var pedido = (await ObtenerPedidoAsync(codigo, ct))!;

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
        var filas = await db.Salidas.Where(s => s.CodigoPedido == codigo).ToListAsync(ct);
        if (filas.Count == 0) return NotFound();
        if (filas[0].Estado != EstadosPedido.EnRevision)
            return Conflict(new ProblemDetails { Status = 409, Title = "No se puede rechazar", Detail = "Solo se puede rechazar un pago en revisión." });

        foreach (var f in filas)
        {
            f.Estado = EstadosPedido.Pendiente;
            f.MotivoRechazo = req.Motivo.Trim();
        }
        await db.SaveChangesAsync(ct);
        return Ok(await ObtenerPedidoAsync(codigo, ct));
    }

    /// <summary>El comprador puede cancelar su pedido mientras no esté confirmado. El stock vuelve a estar disponible.</summary>
    [HttpPost("{codigo}/cancelar")]
    [Authorize(Roles = Roles.Comprador)]
    public async Task<ActionResult<PedidoDto>> Cancelar(string codigo, CancellationToken ct)
    {
        var clienteId = await ClienteDelUsuarioAsync(User.ObtenerUsuarioId(), ct);
        var info = await db.Salidas.AsNoTracking()
            .Where(s => s.CodigoPedido == codigo)
            .Select(s => new { s.ClienteId, s.Estado })
            .FirstOrDefaultAsync(ct);

        if (info is null) return NotFound();
        if (info.ClienteId != clienteId) return Forbid();
        if (info.Estado is not (EstadosPedido.Pendiente or EstadosPedido.EnRevision))
            return Conflict(new ProblemDetails { Status = 409, Title = "No se puede cancelar", Detail = "El pago ya fue confirmado. Comunicate con la tienda." });

        await movimientos.CambiarEstadoAsync(codigo, EstadosPedido.Cancelado, ct);
        return Ok(await ObtenerPedidoAsync(codigo, ct));
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

    /// <summary>Pagina por pedido (no por fila): primero obtiene los códigos de la página y después sus productos.</summary>
    private async Task<PagedResult<PedidoDto>> ListarAsync(PedidoQuery q, CancellationToken ct)
    {
        var query = db.Salidas.AsNoTracking().AsQueryable();

        if (q.ClienteId is int clienteId) query = query.Where(s => s.ClienteId == clienteId);
        if (!string.IsNullOrWhiteSpace(q.Estado)) query = query.Where(s => s.Estado == q.Estado);
        if (!string.IsNullOrWhiteSpace(q.Buscar))
        {
            var b = q.Buscar.Trim();
            query = query.Where(s => s.CodigoPedido.Contains(b) || s.Cliente.Nombre.Contains(b) || s.Cliente.Documento.Contains(b));
        }

        var codigos = query
            .GroupBy(s => s.CodigoPedido)
            .Select(g => new { Codigo = g.Key, Fecha = g.Max(s => s.Fecha) })
            .OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Codigo);

        var pagina = Math.Max(1, q.Pagina);
        var tamanio = Math.Clamp(q.Tamanio, 1, PaginacionQuery.TamanioMaximo);
        var total = await codigos.CountAsync(ct);
        var codigosPagina = await codigos
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .Select(x => x.Codigo)
            .ToListAsync(ct);

        var pedidos = await CargarPedidosAsync(codigosPagina, ct);
        var ordenados = codigosPagina.Select(c => pedidos[c]).ToList();

        return new PagedResult<PedidoDto>(ordenados, pagina, tamanio, total, (int)Math.Ceiling(total / (double)tamanio));
    }

    private async Task<PedidoDto?> ObtenerPedidoAsync(string codigo, CancellationToken ct)
    {
        var pedidos = await CargarPedidosAsync([codigo], ct);
        return pedidos.GetValueOrDefault(codigo);
    }

    private async Task<Dictionary<string, PedidoDto>> CargarPedidosAsync(List<string> codigos, CancellationToken ct)
    {
        if (codigos.Count == 0) return new Dictionary<string, PedidoDto>();

        var filas = await db.Salidas.AsNoTracking()
            .Where(s => codigos.Contains(s.CodigoPedido))
            .OrderBy(s => s.Id)
            .Select(s => new
            {
                s.Id, s.CodigoPedido, s.Fecha, s.Estado, s.ClienteId, s.Observaciones,
                s.ComprobanteArchivo, s.ComprobanteFecha, s.MotivoRechazo,
                Cliente = s.Cliente.Nombre, s.Cliente.Telefono, s.Cliente.Direccion,
                s.ProductoId, Producto = s.Producto.Nombre, s.Producto.ImagenUrl,
                s.Cantidad, s.PrecioUnitario, s.Total
            })
            .ToListAsync(ct);

        return filas
            .GroupBy(f => f.CodigoPedido)
            .ToDictionary(g => g.Key, g =>
            {
                var primera = g.First();
                var items = g.Select(f => new PedidoItemDto(f.Id, f.ProductoId, f.Producto, f.ImagenUrl, f.Cantidad, f.PrecioUnitario, f.Total)).ToList();
                return new PedidoDto(
                    g.Key, primera.Fecha, primera.Estado, EstadosPedido.Texto(primera.Estado),
                    primera.ClienteId, primera.Cliente, primera.Telefono, primera.Direccion,
                    items.Sum(i => i.Subtotal), items.Sum(i => i.Cantidad), primera.Observaciones,
                    primera.ComprobanteArchivo is not null, primera.ComprobanteFecha, primera.MotivoRechazo,
                    items);
            });
    }
}
