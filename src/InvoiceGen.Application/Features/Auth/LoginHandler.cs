using ErrorOr;
using InvoiceGen.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace InvoiceGen.Application.Features.Auth;

public sealed class LoginHandler(
    UserManager<AppUser> userManager,
    AuthTokenIssuer tokenIssuer)
{
    public async Task<ErrorOr<AuthResponse>> HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Error.Unauthorized("invalid_credentials", "Incorrect email or password.");

        // A deleted account must be indistinguishable from a non-existent one — never leak
        // "this account was deleted" (same reasoning as the account-lockout 401). In practice
        // DELETE /auth/me also tombstones the email, so FindByEmailAsync won't even find it;
        // this is the explicit belt-and-suspenders guard on the deleted state itself.
        if (user.DeletedAt is not null)
            return Error.Unauthorized("invalid_credentials", "Incorrect email or password.");

        // A Google-only account (created via POST /auth/google) has no password. Rather than
        // let CheckPasswordAsync fail with a generic "wrong password", surface a distinct code
        // so the frontend can steer the user to "Continue with Google". Deliberately NOT the
        // non-leaking 401 the deleted/lockout cases use — see x-google-auth-policy.
        if (user.PasswordHash is null)
            return Error.Conflict("account_uses_google_auth",
                "This account was created with Google. Continue with Google to sign in.");

        if (await userManager.IsLockedOutAsync(user))
            return Error.Unauthorized("account_locked",
                "This account is temporarily locked after too many failed attempts. Try again later.");

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user); // increments; locks at the threshold
            return Error.Unauthorized("invalid_credentials", "Incorrect email or password.");
        }

        await userManager.ResetAccessFailedCountAsync(user); // clear the counter on success

        return await tokenIssuer.IssueAsync(user, cancellationToken);
    }
}
