using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MAAT.Application.Interfaces;
using MAAT.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MAAT.Infrastructure.Security;

public class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user)
    {
        var opts = options.Value;
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddSeconds(opts.AccessTokenLifetimeSeconds);

        // Claims volontairement limitées à sub/company_id/role/jti : le JWT est signé,
        // pas chiffré, et son contenu est lisible par quiconque l'intercepte.
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("company_id", user.CompanyId.ToString()),
            new Claim("role", user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: opts.Issuer,
            audience: opts.Audience,
            claims: claims,
            notBefore: null,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return (tokenString, expiresAt);
    }
}
