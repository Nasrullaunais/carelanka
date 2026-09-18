using System.Text.RegularExpressions;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed partial class BedSuggestionRequestValidator : AbstractValidator<BedSuggestionRequest>
{
    public BedSuggestionRequestValidator()
    {
        // Stated as a rule rather than left to a silent precedence: a caller who sends both should
        // be told which one the server ignored, and the honest answer is that it would not know.
        RuleFor(request => request)
            .Must(ExactlyOneIdentifier)
            .WithName("admission_id")
            .WithMessage(
                "Send either an admission_id, for somebody already on the patients board, "
                + "or a patient_identifier from the hospital slip - one of the two, not both.");

        RuleFor(request => request.AdmissionId)
            .NotEqual(Guid.Empty)
            .When(request => request.AdmissionId.HasValue);

        RuleFor(request => request.PatientIdentifier!)
            .MaximumLength(32)
            .Must(BeAnIdentifier)
            .WithMessage(
                "That is not an NIC or a patient code. "
                + PatientIdentifierFormats.PatientCodeMessage)
            .When(request => !string.IsNullOrWhiteSpace(request.PatientIdentifier));
    }

    private static bool ExactlyOneIdentifier(BedSuggestionRequest request)
    {
        var hasAdmission = request.AdmissionId.HasValue;
        var hasIdentifier = !string.IsNullOrWhiteSpace(request.PatientIdentifier);

        return hasAdmission ^ hasIdentifier;
    }

    /// <summary>
    /// Either format is fine and the caller does not say which. Checked here only so an obvious
    /// typo is a 400 rather than a workflow row and a model call that were always going to come
    /// back "no such patient".
    /// </summary>
    private static bool BeAnIdentifier(string identifier)
    {
        var trimmed = identifier.Trim();

        return PatientCodePattern().IsMatch(trimmed) || NicPattern().IsMatch(trimmed);
    }

    [GeneratedRegex(PatientIdentifierFormats.PatientCode, RegexOptions.IgnoreCase)]
    private static partial Regex PatientCodePattern();

    [GeneratedRegex(PatientIdentifierFormats.Nic)]
    private static partial Regex NicPattern();
}
