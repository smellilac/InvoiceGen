using ErrorOr;
using InvoiceGen.Application.Features.Auth;

namespace InvoiceGen.Tests.Features.Auth;

// Stands in for the real Google.Apis.Auth verification in tests — we must never call Google.
// It treats the "id token" as a simple encoding so a single registered instance can drive
// every scenario without mutation:
//   "invalid-token"            -> verification fails (as an expired/tampered/wrong-audience token would)
//   "{sub}|{email}|{verified}" -> a successfully verified payload
internal sealed class FakeGoogleTokenVerifier : IGoogleTokenVerifier
{
    public const string InvalidToken = "invalid-token";

    public static string Token(string subject, string email, bool emailVerified = true) =>
        $"{subject}|{email}|{(emailVerified ? "true" : "false")}";

    public Task<ErrorOr<GoogleUserInfo>> VerifyAsync(string idToken, CancellationToken cancellationToken)
    {
        var parts = idToken.Split('|');
        if (idToken == InvalidToken || parts.Length != 3)
            return Task.FromResult<ErrorOr<GoogleUserInfo>>(
                Error.Unauthorized("invalid_google_token", "The Google ID token is missing or invalid."));

        return Task.FromResult<ErrorOr<GoogleUserInfo>>(
            new GoogleUserInfo(parts[0], parts[1], bool.Parse(parts[2])));
    }
}
