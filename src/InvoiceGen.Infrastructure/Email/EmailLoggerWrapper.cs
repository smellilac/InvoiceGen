using Microsoft.Extensions.Logging;

namespace InvoiceGen.Infrastructure.Email;

// Source-generated log messages for the email-delivery pipeline. Centralizes the templates,
// levels, and event ids so call sites stay allocation-free and consistent. Add new messages
// here rather than calling ILogger.Log* directly.
internal static partial class EmailLoggerWrapper
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error,
        Message = "Unhandled error processing send for document {DocumentId}")]
    public static partial void SendProcessingFailed(ILogger logger, Exception ex, Guid documentId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Send failed for document {DocumentId}")]
    public static partial void SendFailed(ILogger logger, Exception ex, Guid documentId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "EMAIL (placeholder) → {To} | subject: {Subject} | attachment: {File} ({Bytes} bytes)")]
    public static partial void PlaceholderSend(ILogger logger, string to, string subject, string file, int bytes);
}
