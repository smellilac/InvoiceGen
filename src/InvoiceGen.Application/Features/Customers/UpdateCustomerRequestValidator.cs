using FluentValidation;

namespace InvoiceGen.Application.Features.Customers;

public sealed class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        // Reject a no-op PATCH: at least one field must be supplied (non-null).
        RuleFor(x => x)
            .Must(HasAtLeastOneField)
            .WithMessage("At least one field must be provided.");

        // If Name is supplied it can't be blanked (required column) and must fit.
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name cannot be empty.")
            .MaximumLength(CustomerLimits.NameMax)
            .When(x => x.Name is not null);

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

    private static bool HasAtLeastOneField(UpdateCustomerRequest r) =>
        r.Name is not null
        || r.Email is not null
        || r.Address is not null
        || r.Phone is not null
        || r.Notes is not null;
}
