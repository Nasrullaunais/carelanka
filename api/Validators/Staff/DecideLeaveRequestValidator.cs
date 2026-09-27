using CareLanka.Api.DTOs.Staff;
using FluentValidation;

namespace CareLanka.Api.Validators.Staff;

public sealed class DecideLeaveRequestValidator : AbstractValidator<DecideLeaveRequest>
{
    private static readonly HashSet<string> ValidDecisions = new(StringComparer.OrdinalIgnoreCase)
    {
        "approve",
        "reject"
    };

    public DecideLeaveRequestValidator()
    {
        RuleFor(x => x.Decision)
            .NotEmpty().WithMessage("Decision is required.")
            .Must(decision => !string.IsNullOrWhiteSpace(decision) && ValidDecisions.Contains(decision.Trim()))
            .WithMessage("Decision must be either 'approve' or 'reject'.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes must not exceed 500 characters.");
    }
}
