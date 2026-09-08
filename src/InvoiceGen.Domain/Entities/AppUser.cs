using Microsoft.AspNetCore.Identity;

namespace InvoiceGen.Domain.Entities;

// Extends ASP.NET Core Identity's user with our business-profile fields.
// Identity supplies Id (Guid), Email, UserName, PasswordHash, etc.

// TODO that class provoke leak of the Asp .net Identity package into domain, but I am not sure I need fix this
public class AppUser : IdentityUser<Guid>
{
    public string? BusinessName { get; set; }
    public string? BusinessAddress { get; set; }
    public string? LogoUrl { get; set; }
    // The current uploaded logo's opaque id (see UserLogo). Null when the logo is unset or
    // was set to an external URL via PATCH /auth/me. LogoUrl is what the frontend renders;
    // LogoId lets the server find the stored bytes (e.g. for the PDF) and rotate them.
    public Guid? LogoId { get; set; }
    public string? DefaultCurrency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
