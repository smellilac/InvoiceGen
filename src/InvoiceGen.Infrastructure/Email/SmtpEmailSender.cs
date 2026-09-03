using InvoiceGen.Application.Features.Documents;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace InvoiceGen.Infrastructure.Email;

// Sends via an SMTP relay (e.g. Brevo) using MailKit. A fresh SmtpClient per send keeps this
// stateless and thread-safe, so it's safe as a singleton. Throws on failure — the worker
// catches it and records the send as failed (MarkSendFailed).
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        email.To.Add(MailboxAddress.Parse(message.ToEmail));
        email.Subject = message.Subject;

        var body = new BodyBuilder { TextBody = message.Body };
        body.Attachments.Add(message.PdfFileName, message.PdfContent, ContentType.Parse("application/pdf"));
        email.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port, SecureSocketOptions.StartTls, cancellationToken);
        await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        await client.SendAsync(email, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
