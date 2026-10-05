using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public abstract class EmergencyReportRangeValidator<T> : AbstractValidator<T> where T : EmergencyReportRequest
{
    // Midnight in hospital time on the very first or last calendar day falls outside what DateTimeOffset can hold.
    private static readonly DateOnly FirstReportableDay = DateOnly.MinValue.AddDays(1);
    private static readonly DateOnly LastReportableDay = DateOnly.MaxValue.AddDays(-1);

    protected EmergencyReportRangeValidator()
    {
        RuleFor(request => request.From)
            .NotNull()
            .InclusiveBetween(FirstReportableDay, LastReportableDay);
        RuleFor(request => request.To)
            .NotNull()
            .InclusiveBetween(FirstReportableDay, LastReportableDay)
            .GreaterThanOrEqualTo(request => request.From)
            .When(request => request.From.HasValue, ApplyConditionTo.CurrentValidator);
    }
}

public sealed class EmergencyReportRequestValidator : EmergencyReportRangeValidator<EmergencyReportRequest>;

public sealed class ResponseTimeReportRequestValidator : EmergencyReportRangeValidator<ResponseTimeReportRequest>;
