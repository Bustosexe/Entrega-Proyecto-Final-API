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
/// El comprador compra desde la app (queda Pendiente con el stock reservado) y el admin lo confirma, entrega o cancela.
/// </summary>
[ApiController]
[Route("api/pedidos")]
[Authorize(Roles = Roles.Todos)]
[Produces("application/json")]
public class PedidosController(AppDbContext db, IMovimientoService movimientos) : ControllerBase
{
    /// <summary>
    /// Confirma el carrito. Comprador: compra para sí mismo y el pedido queda Pendiente.
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

        if (User.IsInRole(Roles.Admin))
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

        var pedido = await ObtenerPedidoAsync(codigo, ct);
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

        if (!User.IsInRole(Roles.Admin))
        {
            var clienteId = await ClienteDelUsuarioAsync(User.ObtenerUsuarioId(), ct);
            if (pedido.ClienteId != clienteId) return Forbid();
        }
        return Ok(pedido);
    }

    /// <summary>Admin: Pendiente → Confirmado → Entregado, o Cancelado (devuelve el stock).</summary>
    [HttpPut("{codigo}/estado")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PedidoDto>> CambiarEstado(string codigo, CambiarEstadoRequest req, CancellationToken ct)
    {
        await movimientos.CambiarEstadoAsync(codigo, req.Estado, ct);
        return Ok(await ObtenerPedidoAsync(codigo, ct));
    }

    /// <summary>El comprador puede cancelar su pedido mientras esté Pendiente. El stock vuelve a estar disponible.</summary>
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
        if (info.Estado != EstadosPedido.Pendiente)
            return Conflict(new ProblemDetails { Status = 409, Title = "No se puede cancelar", Detail = "Solo se pueden cancelar pedidos pendientes. Comunicate con la tienda." });

        await movimientos.CambiarEstadoAsync(codigo, EstadosPedido.Cancelado, ct);
        return Ok(await ObtenerPedidoAsync(codigo, ct));
    }

    // ---------------------------------------------------------------------

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
                    g.Key, primera.Fecha, primera.Estado, primera.ClienteId, primera.Cliente, primera.Telefono, primera.Direccion,
                    items.Sum(i => i.Subtotal), items.Sum(i => i.Cantidad), primera.Observaciones, items);
            });
    }
}
