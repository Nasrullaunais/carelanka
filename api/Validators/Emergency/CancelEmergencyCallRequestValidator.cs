using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class CancelEmergencyCallRequestValidator : AbstractValidator<CancelEmergencyCallRequest>
{
    public static readonly EmergencyCallOutcome[] CancelOutcomes =
    [
        EmergencyCallOutcome.FalseAlarm,
        EmergencyCallOutcome.DuplicateCall,
        EmergencyCallOutcome.CallerCancelled,
        EmergencyCallOutcome.NoLongerNeeded
    ];

    public CancelEmergencyCallRequestValidator()
    {
        RuleFor(request => request.Outcome)
            .NotNull()
            .Must(outcome => outcome is null || CancelOutcomes.Contains(outcome.Value))
            .WithMessage("A cancelled call must end as false alarm, duplicate call, caller cancelled or no longer needed.");
        RuleFor(request => request.Notes).MaximumLength(500);
    }
}
