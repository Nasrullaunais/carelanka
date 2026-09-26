using System.Text.RegularExpressions;
using CareLanka.Api.DTOs.Staff;
using FluentValidation;

namespace CareLanka.Api.Validators.Staff;

public sealed partial class BulkShiftRequestValidator : AbstractValidator<BulkShiftRequest>
{
    private static readonly HashSet<string> ValidWeekdays = new(StringComparer.OrdinalIgnoreCase)
    {
        "mon", "tue", "wed", "thu", "fri", "sat", "sun"
    };

    public BulkShiftRequestValidator()
    {
        RuleFor(x => x.WardId)
            .NotEmpty().WithMessage("Ward ID is required.");

        RuleFor(x => x.From)
            .NotEmpty().WithMessage("From date is required.");

        RuleFor(x => x.To)
            .NotEmpty().WithMessage("To date is required.")
            .Must((request, to) => to >= request.From)
            .WithMessage("To date must be greater than or equal to From date.");

        RuleFor(x => x.Weekdays)
            .Must(weekdays => weekdays == null || weekdays.All(d => !string.IsNullOrWhiteSpace(d) && ValidWeekdays.Contains(d)))
            .WithMessage("Weekdays must be one of: mon, tue, wed, thu, fri, sat, sun.");

        RuleFor(x => x.Patterns)
            .NotEmpty().WithMessage("At least one shift pattern is required.")
            .Must(patterns => patterns != null && patterns.Count > 0)
            .WithMessage("At least one shift pattern is required.");

        RuleForEach(x => x.Patterns)
            .SetValidator(new BulkShiftPatternItemValidator());
    }

    public sealed partial class BulkShiftPatternItemValidator : AbstractValidator<BulkShiftPatternItem>
    {
        private static readonly Regex TimeRegex = GeneratedTimeRegex();

        public BulkShiftPatternItemValidator()
        {
            RuleFor(x => x.StartTime)
                .NotEmpty().WithMessage("Start time is required.")
                .Must(BeValidTime).WithMessage("Start time must be in HH:mm 24-hour format.");

            RuleFor(x => x.EndTime)
                .NotEmpty().WithMessage("End time is required.")
                .Must(BeValidTime).WithMessage("End time must be in HH:mm 24-hour format.");

            RuleFor(x => x)
                .Must(x => x.StartTime != x.EndTime)
                .When(x => BeValidTime(x.StartTime) && BeValidTime(x.EndTime))
                .WithMessage("Start time and end time cannot be identical.");

            RuleFor(x => x.RequiredRole)
                .IsInEnum().WithMessage("A valid staff role is required.");

            RuleFor(x => x.HeadcountNeeded)
                .GreaterThanOrEqualTo(1).WithMessage("Headcount needed must be at least 1.");

            RuleFor(x => x.MinimumHeadcount)
                .GreaterThanOrEqualTo(1)
                .When(x => x.MinimumHeadcount.HasValue)
                .WithMessage("Minimum headcount must be at least 1.");

            RuleFor(x => x)
                .Must(x => !x.MinimumHeadcount.HasValue || x.MinimumHeadcount.Value <= x.HeadcountNeeded)
                .WithMessage("Minimum headcount must not exceed headcount needed.");
        }

        private static bool BeValidTime(string? time)
        {
            if (string.IsNullOrWhiteSpace(time))
            {
                return false;
            }

            return TimeRegex.IsMatch(time);
        }

        [GeneratedRegex(@"^([01]\d|2[0-3]):[0-5]\d$")]
        private static partial Regex GeneratedTimeRegex();
    }
}
