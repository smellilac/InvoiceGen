using InvoiceGen.Domain.Entities;

namespace InvoiceGen.Application.Features.Auth;

// The single place that turns an authenticated AppUser into the AuthResponse (access token +
// refresh token + user) returned by every sign-in path — login, register, and Google sign-in.
// Kept as one method so those paths can't drift apart (e.g. a token issued with a different
// lifetime, or a refresh token minted in one path but not another).
public sealed class AuthTokenIssuer(IJwtTokenGenerator jwt, IRefreshTokenService refresh)
{
    public async Task<AuthResponse> IssueAsync(AppUser user, CancellationToken cancellationToken)
    {
        var (accessToken, expiresIn) = jwt.GenerateAccessToken(user);
        var refreshToken = await refresh.IssueAsync(user.Id, cancellationToken);
        return new AuthResponse(accessToken, refreshToken, "Bearer", expiresIn, AppUserDto.FromEntity(user));
    }
}
