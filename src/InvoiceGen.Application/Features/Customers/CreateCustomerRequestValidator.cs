using FluentValidation;

namespace InvoiceGen.Application.Features.Customers;

public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(CustomerLimits.NameMax);

        // Optional fields: only validated when supplied (MaximumLength passes on null).
        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .MaximumLength(CustomerLimits.EmailMax)
                .EmailAddress().WithMessage("Email must be a valid email address.");
        });

        RuleFor(x => x.Address).MaximumLength(CustomerLimits.AddressMax);
        RuleFor(x => x.Phone).MaximumLength(CustomerLimits.PhoneMax);
        RuleFor(x => x.Notes).MaximumLength(CustomerLimits.NotesMax);
    }
}
