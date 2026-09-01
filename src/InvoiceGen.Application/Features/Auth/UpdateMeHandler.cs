using ErrorOr;
using InvoiceGen.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace InvoiceGen.Application.Features.Auth;

public sealed class UpdateMeHandler(UserManager<AppUser> userManager)
{
    public async Task<ErrorOr<AppUserDto>> HandleAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Error.Unauthorized("unauthorized", "The current user no longer exists.");

        // PATCH semantics: only overwrite fields the caller actually supplied.
        if (request.BusinessName is not null) user.BusinessName = request.BusinessName;
        if (request.BusinessAddress is not null) user.BusinessAddress = request.BusinessAddress;
        if (request.LogoUrl is not null) user.LogoUrl = request.LogoUrl;
        if (request.DefaultCurrency is not null) user.DefaultCurrency = request.DefaultCurrency;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return result.Errors.Select(e => Error.Validation(e.Code, e.Description)).ToList();

        return AppUserDto.FromEntity(user);
    }
}
