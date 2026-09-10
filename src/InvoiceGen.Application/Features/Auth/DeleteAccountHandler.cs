using ErrorOr;
using InvoiceGen.Application.Common;
using InvoiceGen.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Auth;

// Permanently deletes the caller's own account (DELETE /auth/me). Irreversible: there is no
// undo endpoint and no grace period. Everything is soft-deleted via the existing DeletedAt
// convention so history/rows survive, but the account is unusable from that moment on.
public sealed class DeleteAccountHandler(
    IAppDbContext db,
    UserManager<AppUser> userManager,
    IRefreshTokenService refresh,
    TimeProvider clock)
{
    public async Task<ErrorOr<Deleted>> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.DeletedAt is not null)
            return Error.NotFound("user_not_found", "User not found.");

        var now = clock.GetUtcNow();

        // 1. Log out everywhere, immediately — revoke ALL of this user's refresh tokens, not
        //    just the one on the current request. Access tokens are stateless and short-lived
        //    (~15 min) so they can't be revoked; they lapse on their own.
        await refresh.RevokeAllForUserAsync(userId, cancellationToken);

        // 2. Soft-delete the user's data. Documents must be hidden BEFORE we return: the global
        //    query filter (DeletedAt == null) is exactly what the email-sending background worker
        //    honors when it reloads a queued document by id, so a soft-deleted document turns any
        //    already-enqueued send into a no-op. That closes the "a background job could still act
        //    on this user after deletion" gap — there's no stale in-memory state left to fire.
        await db.Documents
            .Where(d => d.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.DeletedAt, now), cancellationToken);

        await db.Customers
            .Where(c => c.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.DeletedAt, now), cancellationToken);

        // 3. Soft-delete the user row AND release its email/username. The unique index on
        //    NormalizedUserName (UserName == Email here) would otherwise block the same person
        //    from ever registering again; tombstoning frees the address deliberately, per the
        //    decision that a soft-deleted user's email is available again for POST /auth/register.
        user.DeletedAt = now;
        var tombstone = $"deleted+{user.Id:N}@deleted.invoicegen.local";
        user.Email = tombstone;
        user.NormalizedEmail = userManager.NormalizeEmail(tombstone);
        user.UserName = tombstone;
        user.NormalizedUserName = userManager.NormalizeName(tombstone);

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return result.Errors.Select(e => Error.Failure(e.Code, e.Description)).ToList();

        return Result.Deleted;
    }
}
