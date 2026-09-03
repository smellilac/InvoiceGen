namespace InvoiceGen.Application.Features.Documents;

// A queued send, processed asynchronously by the background worker.
public sealed record EmailSendJob(Guid DocumentId, string ToEmail, string? Message);

public sealed record EmailMessage(
    string ToEmail,
    string Subject,
    string Body,
    byte[] PdfContent,
    string PdfFileName);

// Enqueue a send for async processing (in-process Channel + BackgroundService worker).
public interface IEmailQueue
{
    ValueTask EnqueueAsync(EmailSendJob job, CancellationToken cancellationToken);
}

// Sends an email; THROWS on failure (the worker records it as a failed send). The concrete
// provider (SMTP / SES / SendGrid) is an Infrastructure detail behind this interface.
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
