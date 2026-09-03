using InvoiceGen.Application.Features.Documents;
using Microsoft.Extensions.Logging;

namespace InvoiceGen.Infrastructure.Email;

// Placeholder sender: logs the send and succeeds. Replace with a real SMTP / SES / SendGrid
// implementation of IEmailSender — nothing else in the pipeline changes.
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        EmailLoggerWrapper.PlaceholderSend(
            logger, message.ToEmail, message.Subject, message.PdfFileName, message.PdfContent.Length);
        return Task.CompletedTask;
    }
}
