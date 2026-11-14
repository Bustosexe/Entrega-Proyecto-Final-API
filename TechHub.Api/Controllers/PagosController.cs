using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TechHub.Api.DTOs;
using TechHub.Api.Services;

namespace TechHub.Api.Controllers;

/// <summary>Datos para pagar por transferencia (configurables en la sección "Pagos" de appsettings).</summary>
[ApiController]
[Route("api/pagos")]
[Produces("application/json")]
public class PagosController(IOptions<PagosOptions> opciones) : ControllerBase
{
    /// <summary>Alias, CBU, titular y banco de la tienda. Público: se muestra en el carrito antes de comprar.</summary>
    [HttpGet("transferencia")]
    [AllowAnonymous]
    public ActionResult<DatosTransferenciaDto> DatosTransferencia()
    {
        var o = opciones.Value;
        return Ok(new DatosTransferenciaDto(o.Alias, o.Cbu, o.Titular, o.Cuit, o.Banco, o.HorasReserva));
    }
}
