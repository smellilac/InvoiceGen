using ErrorOr;
using InvoiceGen.Application.Common;
using InvoiceGen.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Auth;

// Clears the user's uploaded logo: removes the stored bytes and nulls LogoId/LogoUrl.
// Returns the refreshed profile so the frontend can drop it straight into its auth state
// (same shape as GET/PATCH /auth/me), mirroring the upload response.
//
// (PATCH /auth/me with logo_url: "" is the other way to clear the URL; that path only
// blanks LogoUrl and leaves the stored bytes, whereas this endpoint fully removes them.)
public sealed class DeleteLogoHandler(UserManager<AppUser> userManager, IAppDbContext db)
{
    public async Task<ErrorOr<AppUserDto>> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Error.Unauthorized("unauthorized", "The current user no longer exists.");

        var existing = await db.UserLogos.Where(l => l.UserId == userId).ToListAsync(cancellationToken);
        db.UserLogos.RemoveRange(existing);

        user.LogoId = null;
        user.LogoUrl = null;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return result.Errors.Select(e => Error.Validation(e.Code, e.Description)).ToList();

        return AppUserDto.FromEntity(user);
    }
}
