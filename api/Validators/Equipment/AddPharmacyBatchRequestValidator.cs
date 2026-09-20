using CareLanka.Api.DTOs.Equipment;
using FluentValidation;

namespace CareLanka.Api.Validators.Equipment;

public sealed class AddPharmacyBatchRequestValidator : AbstractValidator<AddPharmacyBatchRequest>
{
    public AddPharmacyBatchRequestValidator()
    {
        RuleFor(request => request.Quantity)
            .GreaterThan(0).WithMessage("Enter how many arrived.");

        RuleFor(request => request.Reference).MaximumLength(50);
        RuleFor(request => request.Note).MaximumLength(300);
    }
}
