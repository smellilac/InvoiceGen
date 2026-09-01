namespace InvoiceGen.Domain.Entities;

// A long-lived refresh token, stored HASHED (never raw). Revocable so that
// logout truly ends a session — see docs/decisions-log.md.
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
