using CareLanka.Api.DTOs.Staff;
using FluentValidation;

namespace CareLanka.Api.Validators.Staff;

public sealed class WardStaffingRuleInputValidator : AbstractValidator<WardStaffingRuleInput>
{
    public WardStaffingRuleInputValidator()
    {
        RuleFor(x => x.RequiredRole)
            .IsInEnum().WithMessage("A valid staff role is required.");

        RuleFor(x => x.MinimumHeadcount)
            .GreaterThanOrEqualTo(1).WithMessage("Minimum headcount must be at least 1.");
    }
}
