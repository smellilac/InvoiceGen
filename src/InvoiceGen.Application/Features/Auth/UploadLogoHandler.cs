using ErrorOr;
using InvoiceGen.Application.Common;
using InvoiceGen.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Auth;

// Stores an uploaded logo server-side and points the user's LogoUrl at it.
//
// The endpoint (Api) owns the raw-upload concerns it needs HttpContext for — the size
// cap and building the absolute retrieval URL — and hands us the pre-generated logoId and
// its URL. We own validation-of-content (via IImageProcessor) and persistence.
public sealed class UploadLogoHandler(
    UserManager<AppUser> userManager,
    IAppDbContext db,
    IImageProcessor imageProcessor,
    TimeProvider timeProvider)
{
    public async Task<ErrorOr<AppUserDto>> HandleAsync(
        Guid userId, Guid logoId, string logoUrl, byte[] imageBytes, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return Error.Unauthorized("unauthorized", "The current user no longer exists.");

        // Sniff + decode + downscale + re-encode. Rejects non-images (declared type is
        // never trusted) with a 422-mapped validation error.
        var normalized = imageProcessor.Normalize(imageBytes);
        if (normalized.IsError)
            return normalized.Errors;

        // One logo per user: drop any previous row so the old bytes/id don't linger.
        var existing = await db.UserLogos.Where(l => l.UserId == userId).ToListAsync(cancellationToken);
        db.UserLogos.RemoveRange(existing);

        db.UserLogos.Add(new UserLogo
        {
            Id = logoId,
            UserId = userId,
            ImageBytes = normalized.Value.Bytes,
            ContentType = normalized.Value.ContentType,
            CreatedAt = timeProvider.GetUtcNow(),
        });

        user.LogoId = logoId;
        user.LogoUrl = logoUrl;

        // UserManager and IAppDbContext share the same scoped AppDbContext, so this single
        // SaveChanges (via UpdateAsync) flushes the user update AND the UserLogo add/remove
        // tracked above in one transaction.
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return result.Errors.Select(e => Error.Validation(e.Code, e.Description)).ToList();

        return AppUserDto.FromEntity(user);
    }
}
