using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TechHub.Api.Models;

namespace TechHub.Api.Services;

public class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiraMinutos { get; set; } = 120;
}

public interface ITokenService
{
    (string Token, DateTime Expira) GenerarToken(Usuario usuario);
}

/// <summary>
/// Emite el JWT firmado con HMAC-SHA256. El token NO está encriptado: su contenido se puede leer,
/// pero cualquier modificación invalida la firma y el servidor lo rechaza.
/// </summary>
public class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _opt = options.Value;

    public (string Token, DateTime Expira) GenerarToken(Usuario usuario)
    {
        var expira = DateTime.UtcNow.AddMinutes(_opt.ExpiraMinutos);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opt.Issuer,
            Audience = _opt.Audience,
            Expires = expira,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = usuario.Id.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                ["name"] = usuario.NombreUsuario,
                ["role"] = usuario.Rol.Nombre   // el Rol tiene que venir cargado (Include)
            }
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expira);
    }
}
