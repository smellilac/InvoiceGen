using FluentValidation;

namespace InvoiceGen.Application.Features.Documents;

public sealed record SendDocumentRequest(string? ToEmail, string? Message);

public sealed class SendDocumentRequestValidator : AbstractValidator<SendDocumentRequest>
{
    // Not persisted (message → email body, to_email → recipient), so these caps are input
    // hygiene against oversized payloads, not DB column limits.
    private const int ToEmailMax = 254;   // RFC 5321 max email length
    private const int MessageMax = 2000;

    public SendDocumentRequestValidator()
    {
        // Both fields optional; when to_email is supplied it must be a real, bounded address.
        RuleFor(x => x.ToEmail)
            .MaximumLength(ToEmailMax)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.ToEmail))
            .WithMessage("to_email must be a valid email address (max 254 chars).");

        RuleFor(x => x.Message)
            .MaximumLength(MessageMax)
            .WithMessage($"message must be {MessageMax} characters or fewer.");
    }
}
