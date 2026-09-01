using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using InvoiceGen.Application.Features.Auth;
using InvoiceGen.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InvoiceGen.Infrastructure.Auth;

public sealed class JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider clock) : IJwtTokenGenerator
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, int ExpiresInSeconds) GenerateAccessToken(AppUser user)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        // Only the user id (sub) — ownership checks need nothing more. No email:
        // it would go stale in the token if the user changed it. Jti gives each
        // token a unique id (enables denylist/replay-detection later if needed).
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        // expires_in is seconds per the API contract; settings hold minutes for readability.
        return (new JwtSecurityTokenHandler().WriteToken(token), _options.AccessTokenMinutes * 60);
    }
}
