using ErrorOr;

namespace InvoiceGen.Application.Features.Auth;

public sealed class LogoutHandler(IRefreshTokenService refresh)
{
    public async Task<ErrorOr<Success>> HandleAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        await refresh.RevokeAsync(request.RefreshToken, cancellationToken);
        return Result.Success;
    }
}
