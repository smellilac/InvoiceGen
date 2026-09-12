using ErrorOr;
using InvoiceGen.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceGen.Application.Features.Auth;

// "Continue with Google" (POST /auth/google). Verifies the Google ID token, then logs the
// user in — creating or linking a local account as needed — and issues our own token pair via
// the SAME AuthTokenIssuer the email/password paths use. See x-google-auth-policy.
public sealed class GoogleSignInHandler(
    UserManager<AppUser> userManager,
    IGoogleTokenVerifier verifier,
    AuthTokenIssuer tokenIssuer,
    TimeProvider clock,
    ILogger<GoogleSignInHandler> logger)
{
    public async Task<ErrorOr<AuthResponse>> HandleAsync(GoogleSignInRequest request, CancellationToken cancellationToken)
    {
        var verification = await verifier.VerifyAsync(request.IdToken, cancellationToken);
        if (verification.IsError)
            return verification.Errors;

        var google = verification.Value;

        // Never create or link off an email Google itself hasn't confirmed the user owns —
        // that confirmation is the entire basis for trusting the token and auto-linking below.
        if (!google.EmailVerified)
            return Error.Validation("email_not_verified",
                "Your Google account's email address is not verified.");

        // 1. Already linked to this Google identity → straight log in. GoogleId is unique and
        //    cleared on account deletion, so a match here is always a live account.
        var byGoogleId = await userManager.Users
            .FirstOrDefaultAsync(u => u.GoogleId == google.Subject, cancellationToken);
        if (byGoogleId is not null)
            return await tokenIssuer.IssueAsync(byGoogleId, cancellationToken);

        // 2. An existing account with the same (normalized) email. FindByEmailAsync applies the
        //    same normalization the login path uses, and won't return a deleted account (its
        //    email is tombstoned on deletion), so a hit here is a live, unlinked account.
        var byEmail = await userManager.FindByEmailAsync(google.Email);
        if (byEmail is not null)
        {
            // A different Google id is already stored → someone else's identity, or corrupted
            // data. Do NOT silently overwrite the link; surface it as an internal error.
            if (byEmail.GoogleId is not null && byEmail.GoogleId != google.Subject)
            {
                logger.LogError(
                    "Google sign-in: account {UserId} has GoogleId {ExistingGoogleId} but token presented {TokenGoogleId} for the same email",
                    byEmail.Id, byEmail.GoogleId, google.Subject);
                return Error.Failure("google_link_conflict",
                    "This account is already linked to a different Google identity.");
            }

            // Auto-link a password account to Google. Safe with no extra confirmation because
            // Google has verified the email is theirs (see x-google-auth-policy.account_matching).
            byEmail.GoogleId = google.Subject;
            var linkResult = await userManager.UpdateAsync(byEmail);
            if (!linkResult.Succeeded)
                return linkResult.Errors.Select(e => Error.Failure(e.Code, e.Description)).ToList();

            return await tokenIssuer.IssueAsync(byEmail, cancellationToken);
        }

        // 3. Brand-new account: no password (Google-only), email confirmed immediately since
        //    Google already verified it — skip any app-level confirmation flow.
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = google.Email,
            Email = google.Email,
            EmailConfirmed = true,
            GoogleId = google.Subject,
            CreatedAt = clock.GetUtcNow(),
            LockoutEnabled = true
        };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
            return createResult.Errors.Select(e => Error.Validation(e.Code, e.Description)).ToList();

        return await tokenIssuer.IssueAsync(user, cancellationToken);
    }
}
