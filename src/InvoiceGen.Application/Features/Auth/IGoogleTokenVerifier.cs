using ErrorOr;

namespace InvoiceGen.Application.Features.Auth;

// Verifies a Google Sign-In ID token against Google's public keys (signature, expiry,
// issuer, and audience against this app's configured Google client id). The concrete
// implementation lives in Infrastructure (it pulls in the Google.Apis.Auth library); the
// handler depends only on this abstraction. A token that fails verification returns an
// Unauthorized error — the caller never sees a decoded-but-unverified payload.
public interface IGoogleTokenVerifier
{
    Task<ErrorOr<GoogleUserInfo>> VerifyAsync(string idToken, CancellationToken cancellationToken);
}

// The subset of the verified Google token payload the auth flow needs. Subject is Google's
// stable, unique user id (the `sub` claim) — the value stored as AppUser.GoogleId.
public sealed record GoogleUserInfo(string Subject, string Email, bool EmailVerified);
