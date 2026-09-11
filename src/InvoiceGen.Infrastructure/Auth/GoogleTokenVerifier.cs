using ErrorOr;
using Google.Apis.Auth;
using InvoiceGen.Application.Features.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvoiceGen.Infrastructure.Auth;

// Verifies a Google Sign-In ID token with Google.Apis.Auth. GoogleJsonWebSignature.ValidateAsync
// checks the signature against Google's published public keys and validates expiry, issuer, and
// audience (against our configured client id). Anything that fails those checks throws
// InvalidJwtException, which we translate into an Unauthorized error — the unverified payload is
// never returned to the caller.
public sealed class GoogleTokenVerifier(
    IOptions<GoogleAuthOptions> options,
    ILogger<GoogleTokenVerifier> logger) : IGoogleTokenVerifier
{
    private readonly GoogleAuthOptions _options = options.Value;

    public async Task<ErrorOr<GoogleUserInfo>> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return Error.Unauthorized("invalid_google_token", "The Google ID token is missing or invalid.");

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [_options.ClientId]
        };

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            // Expected for an expired/tampered/wrong-audience token — not server-side noise.
            logger.LogInformation(ex, "Google ID token failed verification");
            return Error.Unauthorized("invalid_google_token", "The Google ID token is missing or invalid.");
        }

        return new GoogleUserInfo(payload.Subject, payload.Email, payload.EmailVerified);
    }
}
