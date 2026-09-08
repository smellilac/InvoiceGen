namespace InvoiceGen.Domain.Entities;

// A business-profile logo uploaded from the user's device, stored server-side as bytes
// (Postgres bytea). AppUser.LogoUrl is only 2048 chars — too small for an inline data:
// URI — so the image lives here and LogoUrl points at GET /auth/logo/{Id}. Kept as raw
// bytes so the PDF renderer can read them directly (QuestPDF .Image(byte[])) without an
// outbound HTTP fetch.
//
// One row per user (unique UserId). A re-upload replaces the row with a fresh Id so the
// retrieval URL changes and browser/PDF caches can't mask the new logo.
public class UserLogo
{
    // Opaque, unguessable public id used in GET /auth/logo/{Id}. Deliberately NOT the
    // user id, so logos can't be enumerated across accounts.
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public byte[] ImageBytes { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
