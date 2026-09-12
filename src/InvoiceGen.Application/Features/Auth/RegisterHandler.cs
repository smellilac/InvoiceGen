using ErrorOr;
using InvoiceGen.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace InvoiceGen.Application.Features.Auth;

// TODO maybe wrap into transaction
public sealed class RegisterHandler(
    UserManager<AppUser> userManager,
    AuthTokenIssuer tokenIssuer,
    TimeProvider clock)
{
    public async Task<ErrorOr<AuthResponse>> HandleAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
            return Error.Conflict("email_taken", "An account with this email already exists.");

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email,
            Email = request.Email,
            BusinessName = request.BusinessName,
            CreatedAt = clock.GetUtcNow(),
            LockoutEnabled = true // required for the failed-login lockout to take effect
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return result.Errors.Select(e => Error.Validation(e.Code, e.Description)).ToList();

        return await tokenIssuer.IssueAsync(user, cancellationToken);
    }
}
