using ErrorOr;

namespace InvoiceGen.Application.Features.Auth;

// Decodes, validates, and normalizes an uploaded image. Implemented in Infrastructure
// (ImageSharp) so the Application layer stays free of the imaging library.
//
// This is the authority on "is this really an image": it sniffs the ACTUAL bytes rather
// than trusting the client-declared content type, rejects anything it can't decode as a
// raster image (including SVG — never rendered inline, so no XSS surface), downscales to
// a bounded size, and re-encodes to a single predictable format.
public interface IImageProcessor
{
    ErrorOr<ProcessedImage> Normalize(byte[] input);
}

public sealed record ProcessedImage(byte[] Bytes, string ContentType);
