using System.Text.RegularExpressions;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

public sealed class ClaimByPatientCodeRequestValidator : AbstractValidator<ClaimByPatientCodeRequest>
{
    private static readonly Regex CodeFormat =
        new(PatientIdentifierFormats.PatientCode, RegexOptions.Compiled);

    private static readonly Regex NicFormat =
        new(PatientIdentifierFormats.Nic, RegexOptions.Compiled);

    public ClaimByPatientCodeRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.PatientCode)
            .NotEmpty().WithMessage("Enter the patient code from your hospital slip.")
            .Must(code => CodeFormat.IsMatch(code.Trim().ToUpperInvariant()))
            .WithMessage(PatientIdentifierFormats.PatientCodeMessage);

        RuleFor(request => request.Nic)
            .NotEmpty().WithMessage("Enter the NIC from your hospital slip.")
            .Must(nic => NicFormat.IsMatch(nic.Trim()))
            .WithMessage(PatientIdentifierFormats.NicMessage);
    }
}
