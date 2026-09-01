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
    public string? DefaultCurrency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
