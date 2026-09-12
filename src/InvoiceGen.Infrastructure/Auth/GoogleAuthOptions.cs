namespace InvoiceGen.Infrastructure.Auth;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    // The OAuth 2.0 client id for this app. A Google ID token is only accepted if its audience
    // matches this value. Set via appsettings (dev) or an environment/secret override
    // (GoogleAuth__ClientId) in other environments — never a hardcoded literal in code.
    public string ClientId { get; set; } = "";
}
