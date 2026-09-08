using ErrorOr;
using InvoiceGen.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Auth;

// Loads a stored logo's bytes by its opaque id, for the public GET /auth/logo/{logoId}
// endpoint (an <img> tag can't send a bearer token, so retrieval is unauthenticated —
// the unguessable id is the access control). Unknown id → NotFound (mapped to 404).
public sealed class GetLogoHandler(IAppDbContext db)
{
    public async Task<ErrorOr<LogoFile>> HandleAsync(Guid logoId, CancellationToken cancellationToken)
    {
        var logo = await db.UserLogos
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == logoId, cancellationToken);

        if (logo is null)
            return Error.NotFound("logo.not_found", "No logo exists with that id.");

        return new LogoFile(logo.ImageBytes, logo.ContentType);
    }
}

public sealed record LogoFile(byte[] Bytes, string ContentType);
