using System.Text.RegularExpressions;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

/// <summary>
/// Either of these, never both and never neither - a rule a caller can read, where a silent
/// precedence rule is one they cannot.
/// </summary>
public sealed class BedSuggestionRequestValidator : AbstractValidator<BedSuggestionRequest>
{
    private const string EitherOr =
        "Give either an admission to suggest a bed for, or an NIC or patient code - not both.";

    private static readonly Regex NicFormat =
        new(PatientIdentifierFormats.Nic, RegexOptions.Compiled);

    private static readonly Regex PatientCodeFormat =
        new(PatientIdentifierFormats.PatientCode, RegexOptions.Compiled);

    public BedSuggestionRequestValidator()
    {
        RuleFor(request => request)
            .Must(request => HasAdmission(request) ^ HasIdentifier(request))
            .WithName(nameof(BedSuggestionRequest.AdmissionId))
            .WithMessage(EitherOr);

        RuleFor(request => request.PatientIdentifier)
            .MaximumLength(32)
            .Must(identifier => Recognised(identifier!))
            .WithMessage(
                "That is not an NIC or a patient code. "
                + PatientIdentifierFormats.PatientCodeMessage)
            .When(request => HasIdentifier(request));
    }

    private static bool HasAdmission(BedSuggestionRequest request)
        => request.AdmissionId is { } admissionId && admissionId != Guid.Empty;

    private static bool HasIdentifier(BedSuggestionRequest request)
        => !string.IsNullOrWhiteSpace(request.PatientIdentifier);

    private static bool Recognised(string identifier)
    {
        var trimmed = identifier.Trim();

        return NicFormat.IsMatch(trimmed) || PatientCodeFormat.IsMatch(trimmed.ToUpperInvariant());
    }
}
