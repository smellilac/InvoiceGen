using ErrorOr;
using InvoiceGen.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace InvoiceGen.Application.Features.Auth;

public sealed class LoginHandler(
    UserManager<AppUser> userManager,
    IJwtTokenGenerator jwt,
    IRefreshTokenService refresh)
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

        if (await userManager.IsLockedOutAsync(user))
            return Error.Unauthorized("account_locked",
                "This account is temporarily locked after too many failed attempts. Try again later.");

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user); // increments; locks at the threshold
            return Error.Unauthorized("invalid_credentials", "Incorrect email or password.");
        }

        await userManager.ResetAccessFailedCountAsync(user); // clear the counter on success

        var (accessToken, expiresIn) = jwt.GenerateAccessToken(user);
        var refreshToken = await refresh.IssueAsync(user.Id, cancellationToken);
        return new AuthResponse(accessToken, refreshToken, "Bearer", expiresIn, AppUserDto.FromEntity(user));
    }
}
