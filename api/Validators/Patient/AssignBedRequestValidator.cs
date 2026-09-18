using CareLanka.Api.DTOs.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed class AssignBedRequestValidator : AbstractValidator<AssignBedRequest>
{
    public const int OverrideReasonLength = 500;

    public AssignBedRequestValidator()
    {
        RuleFor(request => request.BedId)
            .NotNull().WithMessage("Choose a bed.")
            .NotEqual(Guid.Empty).WithMessage("Choose a bed.");

        RuleFor(request => request.OverrideReason).MaximumLength(OverrideReasonLength);

        RuleFor(request => request.WorkflowId)
            .NotEqual(Guid.Empty)
            .When(request => request.WorkflowId.HasValue);
    }
}
