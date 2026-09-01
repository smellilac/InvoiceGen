using ErrorOr;

namespace InvoiceGen.Application.Features.Auth;

public interface IRefreshTokenService
{
    // Create a new refresh token for the user; returns the raw token (client keeps it).
    Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken);

    // Validate a raw refresh token and rotate it (revoke the used one). Returns the owning user id.
    Task<ErrorOr<Guid>> ConsumeAsync(string rawToken, CancellationToken cancellationToken);

    // Revoke a raw refresh token (logout). Idempotent — unknown/already-revoked tokens are a no-op.
    Task RevokeAsync(string rawToken, CancellationToken cancellationToken);
}
