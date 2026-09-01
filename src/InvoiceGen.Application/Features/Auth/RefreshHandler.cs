using ErrorOr;
using InvoiceGen.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace InvoiceGen.Application.Features.Auth;

public sealed class RefreshHandler(
    UserManager<AppUser> userManager,
    IJwtTokenGenerator jwt,
    IRefreshTokenService refresh)
{
    public async Task<ErrorOr<TokenPair>> HandleAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var userIdResult = await refresh.ConsumeAsync(request.RefreshToken, cancellationToken);
        if (userIdResult.IsError)
            return userIdResult.Errors;

        var user = await userManager.FindByIdAsync(userIdResult.Value.ToString());
        if (user is null)
            return Error.Unauthorized("invalid_refresh_token", "Refresh token is invalid, expired, or revoked.");

        var (accessToken, expiresIn) = jwt.GenerateAccessToken(user);
        var newRefreshToken = await refresh.IssueAsync(user.Id, cancellationToken);
        return new TokenPair(accessToken, newRefreshToken, "Bearer", expiresIn);
    }
}
