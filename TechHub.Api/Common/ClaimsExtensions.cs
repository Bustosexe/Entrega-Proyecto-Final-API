using System.Security.Claims;

namespace TechHub.Api.Common;

public static class ClaimsExtensions
{
    /// <summary>Id del usuario logueado, tomado del claim "sub" del JWT.</summary>
    public static int ObtenerUsuarioId(this ClaimsPrincipal user)
    {
        var valor = user.FindFirstValue("sub");
        return int.TryParse(valor, out var id)
            ? id
            : throw new UnauthorizedAccessException("El token no contiene el id de usuario.");
    }
}
