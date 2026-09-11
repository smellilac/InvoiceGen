using FluentValidation;

namespace InvoiceGen.Application.Features.Documents;

// Mirrors CreateDocumentRequestValidator, but `from` and `to` are ALWAYS required: a guest has no
// saved business profile to fall back on for `from`, and no customer_id to snapshot `to` from.
public sealed class GuestCreateDocumentRequestValidator : AbstractValidator<GuestCreateDocumentRequest>
{
    public GuestCreateDocumentRequestValidator()
    {
        RuleFor(x => x.From).NotEmpty()
            .WithMessage("The 'from' field is required.");

        RuleFor(x => x.To).NotEmpty()
            .WithMessage("The 'to' field is required.");

        RuleFor(x => x.Date).NotEqual(default(DateOnly))
            .WithMessage("A document date is required.");

        RuleFor(x => x.Items).NotEmpty()
            .WithMessage("At least one line item is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Name).NotEmpty()
                .WithMessage("Each line item needs a name.");
            item.RuleFor(i => i.Quantity).GreaterThan(0)
                .WithMessage("Quantity must be greater than zero.");
            item.RuleFor(i => i.UnitCost).GreaterThanOrEqualTo(0)
                .WithMessage("Unit cost cannot be negative.");
        });
    }
}
