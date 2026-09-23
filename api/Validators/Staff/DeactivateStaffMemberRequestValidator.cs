using CareLanka.Api.DTOs.Staff;
using FluentValidation;

namespace CareLanka.Api.Validators.Staff;

public sealed class DeactivateStaffMemberRequestValidator : AbstractValidator<DeactivateStaffMemberRequest>
{
    public DeactivateStaffMemberRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Deactivation reason is required.")
            .MaximumLength(500).WithMessage("Deactivation reason must not exceed 500 characters.");
    }
}
