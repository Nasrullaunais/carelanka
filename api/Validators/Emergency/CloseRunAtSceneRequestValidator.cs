using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Emergency;
using FluentValidation;

namespace CareLanka.Api.Validators.Emergency;

public sealed class CloseRunAtSceneRequestValidator : AbstractValidator<CloseRunAtSceneRequest>
{
    public static readonly EmergencyCallOutcome[] SceneOutcomes =
    [
        EmergencyCallOutcome.TreatedAtScene,
        EmergencyCallOutcome.RefusedTransport,
        EmergencyCallOutcome.PatientNotFound,
        EmergencyCallOutcome.DeceasedAtScene
    ];

    public CloseRunAtSceneRequestValidator()
    {
        RuleFor(request => request.Outcome)
            .NotNull()
            .Must(outcome => outcome is null || SceneOutcomes.Contains(outcome.Value))
            .WithMessage("A run closed at the scene must end as treated at scene, refused transport, patient not found or deceased at scene.");
        RuleFor(request => request.Notes).MaximumLength(1000);
    }
}
