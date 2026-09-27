using CareLanka.Api.DTOs.Staff;
using FluentValidation;

namespace CareLanka.Api.Validators.Staff;

public sealed class CreateAllocationRequestValidator : AbstractValidator<CreateAllocationRequest>
{
    public CreateAllocationRequestValidator()
    {
        RuleFor(x => x.ShiftId)
            .NotEmpty().WithMessage("Shift ID is required.");

        RuleFor(x => x.StaffMemberId)
            .NotEmpty().WithMessage("Staff member ID is required.");

        RuleFor(x => x.OverrideReason)
            .NotEmpty()
            .When(x => x.Override)
            .WithMessage("Override reason is required when override is enabled.");

        RuleFor(x => x.OverrideReason)
            .MaximumLength(500)
            .WithMessage("Override reason must not exceed 500 characters.");
    }
}
