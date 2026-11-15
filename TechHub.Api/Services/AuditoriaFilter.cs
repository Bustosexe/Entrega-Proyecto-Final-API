using System.Collections;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using TechHub.Api.DTOs;

namespace TechHub.Api.Services;

/// <summary>
/// Filtro global: después de cada operación que modifica datos (POST, PUT, DELETE) y termina bien,
/// deja un registro en la bitácora con quién la hizo, qué entidad tocó y con qué datos (sin contraseñas).
/// También registra los inicios de sesión, tanto correctos como fallidos.
/// </summary>
public class AuditoriaFilter(IAuditoriaService auditoria, ILogger<AuditoriaFilter> logger) : IAsyncActionFilter
{
    private static readonly Dictionary<string, string> Entidades = new()
    {
        ["Auth"] = "Cuenta", ["Categorias"] = "Categoría", ["Marcas"] = "Marca", ["Productos"] = "Producto",
        ["Proveedores"] = "Proveedor", ["Clientes"] = "Cliente", ["Compras"] = "Compra", ["Pedidos"] = "Pedido",
        ["Usuarios"] = "Usuario", ["Ofertas"] = "Oferta", ["Combos"] = "Combo", ["Reportes"] = "Reporte"
    };

    private static readonly Dictionary<string, string> Acciones = new()
    {
        ["Crear"] = "Alta", ["Actualizar"] = "Modificación", ["Desactivar"] = "Baja", ["Eliminar"] = "Eliminación",
        ["SubirImagen"] = "Imagen subida", ["EliminarImagen"] = "Imagen eliminada",
        ["SubirComprobante"] = "Comprobante subido", ["CambiarEstado"] = "Cambio de estado",
        ["RechazarPago"] = "Pago rechazado", ["Cancelar"] = "Pedido cancelado",
        ["Login"] = "Inicio de sesión", ["Registro"] = "Registro de cuenta", ["ActualizarPerfil"] = "Perfil actualizado",
        ["Enviar"] = "Reporte enviado por email"
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var ejecutado = await next();

        var metodo = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo) || HttpMethods.IsOptions(metodo)) return;
        if (ejecutado.Exception is not null && !ejecutado.ExceptionHandled) return;
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor) return;

        try
        {
            var status = (ejecutado.Result as IStatusCodeActionResult)?.StatusCode ?? 200;
            var esLogin = descriptor.ControllerName == "Auth" && descriptor.ActionName == "Login";
            var exito = status is >= 200 and < 300;
            if (!exito && !(esLogin && status == StatusCodes.Status401Unauthorized)) return;

            var valores = (ejecutado.Result as ObjectResult)?.Value;
            var rutaCreado = (ejecutado.Result as CreatedAtActionResult)?.RouteValues;

            // Quién: el usuario logueado, o el que acaba de iniciar sesión / registrarse.
            int? usuarioId = null;
            if (valores is LoginResponse login) usuarioId = login.Usuario.Id;
            else if (context.HttpContext.User.FindFirst("sub")?.Value is { } sub && int.TryParse(sub, out var id)) usuarioId = id;

            var entidad = Entidades.GetValueOrDefault(descriptor.ControllerName, descriptor.ControllerName);
            var accion = Acciones.GetValueOrDefault(descriptor.ActionName, metodo switch
            {
                "POST" => "Alta", "PUT" or "PATCH" => "Modificación", "DELETE" => "Baja", _ => metodo
            });

            string? entidadId = null;
            if (context.RouteData.Values.TryGetValue("id", out var rid)) entidadId = rid?.ToString();
            else if (context.RouteData.Values.TryGetValue("codigo", out var rcodigo)) entidadId = rcodigo?.ToString();
            else if (rutaCreado is not null)
                entidadId = (rutaCreado.TryGetValue("id", out var cid) ? cid : rutaCreado.TryGetValue("codigo", out var cc) ? cc : null)?.ToString();

            if (esLogin)
            {
                if (!exito) accion = "Inicio de sesión fallido";
                entidadId = null;
            }

            var detalle = ArmarDetalle(context.ActionArguments.Values);
            if (esLogin && !exito) detalle = $"Intento con: {(context.ActionArguments.Values.OfType<LoginRequest>().FirstOrDefault()?.Usuario)}";

            await auditoria.RegistrarAsync(usuarioId, accion, entidad, entidadId, detalle,
                context.HttpContext.Connection.RemoteIpAddress?.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el registro de auditoría.");
        }
    }

    // ---------------------------------------------------------------------

    /// <summary>Resume los datos enviados (cuerpos "...Request"), omitiendo contraseñas y archivos.</summary>
    private static string? ArmarDetalle(IEnumerable<object?> argumentos)
    {
        var partes = new List<string>();
        foreach (var arg in argumentos)
        {
            if (arg is null || !arg.GetType().Name.EndsWith("Request", StringComparison.Ordinal)) continue;
            partes.Add(Objeto(arg));
        }
        return partes.Count == 0 ? null : string.Join(" | ", partes);
    }

    private static string Objeto(object o)
    {
        var props = o.GetType().GetProperties()
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0
                        && !p.Name.Contains("pass", StringComparison.OrdinalIgnoreCase)
                        && !p.Name.Contains("contrase", StringComparison.OrdinalIgnoreCase))
            .Select(p => $"{p.Name}={Valor(p.GetValue(o))}");
        return string.Join(", ", props);
    }

    private static string Valor(object? v) => v switch
    {
        null => "",
        string s => s,
        DateTime d => d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormFile f => $"{f.FileName} ({f.Length} bytes)",
        IEnumerable e => "[" + string.Join("; ", e.Cast<object?>().Take(15).Select(x =>
            x is null ? "" : EsSimple(x) ? Valor(x) : "{" + Objeto(x) + "}")) + "]",
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""
    };

    private static bool EsSimple(object x) =>
        x is string or DateTime or DateOnly || x.GetType().IsPrimitive || x.GetType().IsEnum || x is decimal;
}
