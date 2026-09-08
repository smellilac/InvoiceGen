using InvoiceGen.Application.Common;
using InvoiceGen.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvoiceGen.Application.Features.Documents;

// Resolves a document's FROZEN logo URL (see Document.LogoUrl) back to the stored image bytes so
// the PDF renderer can draw it. Internal logos live in UserLogos keyed by the opaque id at the
// end of the URL (…/auth/logo/{id}). If that row is gone (the user re-uploaded or removed their
// logo since the document was created) or the URL is external/unparseable, this returns null and
// the PDF simply renders without a logo rather than failing the render. Shared by the on-demand
// PDF endpoint and the email worker so both stamp the same logo.
public static class DocumentLogoResolver
{
    public static async Task<byte[]?> ResolveAsync(
        IAppDbContext db, Document document, CancellationToken cancellationToken)
    {
        if (!TryExtractLogoId(document.LogoUrl, out var logoId))
            return null;

        return await db.UserLogos.AsNoTracking()
            .Where(l => l.Id == logoId)
            .Select(l => l.ImageBytes)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // The frozen logo URL ends in the UserLogo's opaque id (…/auth/logo/{guid}). Pull the last
    // path segment and parse it. Returns false for null/empty/external URLs that don't end in a
    // Guid, so the caller just skips the logo lookup.
    private static bool TryExtractLogoId(string? logoUrl, out Guid logoId)
    {
        logoId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(logoUrl))
            return false;

        var path = logoUrl.Split('?', '#')[0].TrimEnd('/');
        var lastSegment = path.Split('/')[^1];
        return Guid.TryParse(lastSegment, out logoId);
    }
}
