using CareLanka.Api.DTOs.Staff;
using FluentValidation;

namespace CareLanka.Api.Validators.Staff;

public sealed class LeaveReportParametersValidator : AbstractValidator<LeaveReportParameters>
{
    private static readonly HashSet<string> AllowedGroupBy = new(StringComparer.OrdinalIgnoreCase)
    {
        "staff",
        "type",
        "ward",
        "month"
    };

    public LeaveReportParametersValidator()
    {
        RuleFor(x => x.From)
            .NotNull()
            .WithMessage("From date is required.");

        RuleFor(x => x.To)
            .NotNull()
            .WithMessage("To date is required.")
            .Must((parameters, to) => !parameters.From.HasValue || !to.HasValue || to.Value >= parameters.From.Value)
            .WithMessage("To date must be greater than or equal to From date.");

        RuleFor(x => x.GroupBy)
            .Must(g => string.IsNullOrWhiteSpace(g) || AllowedGroupBy.Contains(g))
            .WithMessage("groupBy must be one of: staff, type, ward, month.")
            .When(x => !string.IsNullOrWhiteSpace(x.GroupBy));
    }
}
