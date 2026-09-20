using ErrorOr;
using InvoiceGen.Application.Features.Auth;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace InvoiceGen.Infrastructure.Imaging;

// ImageSharp-backed image normalizer. The server is the authority on what gets stored:
// it decodes the actual bytes (proving they're a real raster image), downscales to a
// bounded box, and re-encodes to PNG — so a hostile/huge upload can't be stored or served
// at full size, and the PDF always gets a predictable asset.
public sealed class ImageSharpImageProcessor : IImageProcessor
{
    // Longest edge after downscaling. The frontend already downscales for preview; this is
    // the server enforcing the same ceiling regardless of what the client sent.
    private const int MaxDimension = 300;

    // Header-level guard against decompression bombs: reject absurd pixel dimensions before
    // allocating the full bitmap. A small (<5 MB) file can still declare enormous dimensions.
    private const long MaxPixels = 40_000_000; // 40 megapixels

    private static readonly Error InvalidImage =
        Error.Validation("logo.invalid_image", "The uploaded file is not a valid image.");

    public ErrorOr<ProcessedImage> Normalize(byte[] input)
    {
        try
        {
            // Identify reads only the header — cheap, and lets us bail on a pixel bomb
            // before Load allocates the decoded bitmap.
            var info = Image.Identify(input);
            if ((long)info.Width * info.Height > MaxPixels)
                return Error.Validation("logo.too_large", "The image's dimensions are too large.");

            using var image = Image.Load(input);

            // Shrink so the longest side is <= MaxDimension, preserving aspect ratio. Max
            // mode only ever downscales — a smaller logo is left untouched.
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxDimension, MaxDimension),
            }));

            using var output = new MemoryStream();
            image.SaveAsPng(output);
            return new ProcessedImage(output.ToArray(), "image/png");
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException)
        {
            // Not a decodable image (wrong magic bytes, truncated, or an unsupported format
            // like SVG — which we never want to store/serve inline anyway).
            return InvalidImage;
        }
    }
}
