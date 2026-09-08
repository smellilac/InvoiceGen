using InvoiceGen.Application.Features.Documents;
using InvoiceGen.Domain.Entities;
using InvoiceGen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InvoiceGen.Infrastructure.Email;

// Reads queued sends and processes them: render the PDF, send the email, then record the
// outcome on the document (MarkSent / MarkSendFailed). Singleton hosted service — creates a
// DI scope per job for the (scoped) DbContext.
public sealed class EmailSendingWorker(
    EmailQueue queue,
    IServiceScopeFactory scopeFactory,
    IPdfRenderer renderer,
    IEmailSender sender,
    TimeProvider clock,
    ILogger<EmailSendingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                EmailLoggerWrapper.SendProcessingFailed(logger, ex, job.DocumentId);
            }
        }
    }

    
    private async Task ProcessAsync(EmailSendJob job, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var document = await db.Documents
            .Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.Id == job.DocumentId, cancellationToken);
        if (document is null) return; // deleted between enqueue and processing

        try
        {
            // Stamp the same frozen logo the on-demand PDF endpoint uses (see Document.LogoUrl).
            var logo = await DocumentLogoResolver.ResolveAsync(db, document, cancellationToken);
            var pdf = renderer.Render(document, logo);
            var body = string.IsNullOrWhiteSpace(job.Message)
                ? $"Please find your {DocumentTypeApi.ToDisplayName(document.Type).ToLowerInvariant()} attached."
                : job.Message!;
            var label = string.IsNullOrWhiteSpace(document.Number) ? document.Id.ToString() : document.Number!;
            var fileName = $"{DocumentTypeApi.ToApi(document.Type)}-{label}.pdf";

            await sender.SendAsync(new EmailMessage(job.ToEmail, BuildSubject(document), body, pdf, fileName), cancellationToken);
            document.MarkSent(clock.GetUtcNow());
        }
        catch (Exception ex)
        {
            document.MarkSendFailed(Truncate(ex.Message, 500), clock.GetUtcNow());
            EmailLoggerWrapper.SendFailed(logger, ex, document.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    // Auto-generated subject, e.g. "Invoice INV-0042 from Acme Co." (x-email-delivery-policy).
    private static string BuildSubject(Document d)
    {
        var type = DocumentTypeApi.ToDisplayName(d.Type);
        var fromName = d.From.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
        var numberPart = string.IsNullOrWhiteSpace(d.Number) ? string.Empty : $" {d.Number}";
        var fromPart = string.IsNullOrWhiteSpace(fromName) ? string.Empty : $" from {fromName}";
        return $"{type}{numberPart}{fromPart}";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
