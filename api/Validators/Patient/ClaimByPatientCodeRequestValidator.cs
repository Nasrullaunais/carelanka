using System.Text.RegularExpressions;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed class ClaimByPatientCodeRequestValidator : AbstractValidator<ClaimByPatientCodeRequest>
{
    private static readonly Regex CodeFormat =
        new(PatientIdentifierFormats.PatientCode, RegexOptions.Compiled);

    public ClaimByPatientCodeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.PatientCode)
            .NotEmpty().WithMessage("Enter the patient code from your hospital slip.")
            .Must(code => CodeFormat.IsMatch(code.Trim().ToUpperInvariant()))
            .WithMessage(PatientIdentifierFormats.PatientCodeMessage);

        RuleFor(request => request.DateOfBirth)
            .NotNull().WithMessage("Enter your date of birth.")
            .Must(dateOfBirth => dateOfBirth <= Today)
            .WithMessage("A date of birth cannot be in the future.")
            .Must(dateOfBirth => dateOfBirth >= Today.AddYears(-PatientIdentifierFormats.MaxAgeYears))
            .WithMessage(
                $"A date of birth cannot be more than {PatientIdentifierFormats.MaxAgeYears} "
                + "years ago. Check the year.");
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
