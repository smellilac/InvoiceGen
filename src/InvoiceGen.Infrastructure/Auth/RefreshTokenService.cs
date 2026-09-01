using System.Security.Cryptography;
using System.Text;
using ErrorOr;
using InvoiceGen.Application.Features.Auth;
using InvoiceGen.Domain.Entities;
using InvoiceGen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InvoiceGen.Infrastructure.Auth;

public sealed class RefreshTokenService(
    AppDbContext db,
    IOptions<JwtOptions> options,
    TimeProvider clock) : IRefreshTokenService
{
    private readonly JwtOptions _options = options.Value;

    public async Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = clock.GetUtcNow();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(raw),
            ExpiresAt = now.AddDays(_options.RefreshTokenDays),
            CreatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        return raw;
    }

    public async Task<ErrorOr<Guid>> ConsumeAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token is null || !token.IsActiveAt(clock.GetUtcNow()))
            return Error.Unauthorized("invalid_refresh_token", "Refresh token is invalid, expired, or revoked.");

        token.RevokedAt = clock.GetUtcNow(); // rotation: the used token can't be replayed
        await db.SaveChangesAsync(cancellationToken);
        return token.UserId;
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string Hash(string raw)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
