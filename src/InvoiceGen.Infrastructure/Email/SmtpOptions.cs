namespace InvoiceGen.Infrastructure.Email;

// SMTP relay settings. For Brevo: Host=smtp-relay.brevo.com, Port=587,
// Username=your Brevo login, Password=an SMTP key, FromEmail=a verified sender.
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "InvoiceGen";
}
