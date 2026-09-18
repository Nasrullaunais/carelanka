using CareLanka.Api.Data.Configurations.Patient;
using CareLanka.Api.DTOs.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

/// <summary>
/// Length is the only rule there is. These are four boxes a clinician types prose into, so there
/// is no format to enforce - and an empty profile is a legitimate save, not a validation failure.
/// Rules run against the trimmed value so they see exactly the string that gets stored.
/// </summary>
public sealed class UpdateMedicalProfileRequestValidator
    : AbstractValidator<UpdateMedicalProfileRequest>
{
    private const int Long = PatientMedicalProfileConfiguration.LongFieldLength;
    private const int Allergies = PatientMedicalProfileConfiguration.AllergiesLength;

    public UpdateMedicalProfileRequestValidator()
    {
        RuleFor(request => request.KnownConditions)
            .Must(value => Fits(value, Long))
            .WithMessage(TooLong("Known conditions", Long))
            .When(request => !string.IsNullOrWhiteSpace(request.KnownConditions));

        RuleFor(request => request.Allergies)
            .Must(value => Fits(value, Allergies))
            .WithMessage(TooLong("Allergies", Allergies))
            .When(request => !string.IsNullOrWhiteSpace(request.Allergies));

        RuleFor(request => request.CurrentSymptoms)
            .Must(value => Fits(value, Long))
            .WithMessage(TooLong("Current symptoms", Long))
            .When(request => !string.IsNullOrWhiteSpace(request.CurrentSymptoms));

        RuleFor(request => request.RecentSituation)
            .Must(value => Fits(value, Long))
            .WithMessage(TooLong("Recent situation", Long))
            .When(request => !string.IsNullOrWhiteSpace(request.RecentSituation));
    }

    private static string TooLong(string label, int maxLength)
        => $"{label} cannot be longer than {maxLength} characters.";

    private static bool Fits(string? value, int maxLength) => value!.Trim().Length <= maxLength;
}
