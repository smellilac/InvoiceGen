using FluentValidation;

namespace InvoiceGen.Application.Features.Documents;

public sealed class CreateDocumentRequestValidator : AbstractValidator<CreateDocumentRequest>
{
    public CreateDocumentRequestValidator()
    {
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
