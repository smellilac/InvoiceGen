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

    // Google account subject (`sub` claim) for users who signed in via "Continue with
    // Google". Null for password-only accounts. Unique when set (a Google identity maps to
    // at most one local account). PasswordHash is null for Google-only accounts (they have
    // no password); such an account is linked to a password once it sets one, and a
    // password account is linked to Google on first Google sign-in with the same verified
    // email. See POST /auth/google and x-google-auth-policy in docs/openapi.yaml.
    public string? GoogleId { get; set; }

    // SOFT delete (same DeletedAt convention as Customer/Document). Set by DELETE /auth/me,
    // which also tombstones Email/UserName so the address frees up for re-registration.
    // A user with DeletedAt set is treated as gone: login is refused exactly like bad
    // credentials (see LoginHandler) and every refresh token is revoked. Irreversible —
    // there is no un-delete path.
    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsDeleted => DeletedAt is not null;
}
