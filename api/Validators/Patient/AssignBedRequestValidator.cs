using CareLanka.Api.DTOs.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed class AssignBedRequestValidator : AbstractValidator<AssignBedRequest>
{
    public AssignBedRequestValidator()
    {
        RuleFor(request => request.BedId)
            .NotEmpty().WithMessage("Choose a bed.");

        RuleFor(request => request.OverrideReason).MaximumLength(500);
    }
}
