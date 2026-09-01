using ErrorOr;
using InvoiceGen.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace InvoiceGen.Application.Features.Auth;

public sealed class GetMeHandler(UserManager<AppUser> userManager)
{
    public async Task<ErrorOr<AppUserDto>> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Error.Unauthorized("unauthorized", "The current user no longer exists.");

        return AppUserDto.FromEntity(user);
    }
}
