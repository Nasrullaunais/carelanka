using CareLanka.Api.DTOs.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

/// <summary>
/// Every field is optional because this is a patch - blank means "leave it alone", which is how
/// the service reads it. When only one of NIC and date of birth is sent, the service checks it
/// against the other one already on the record.
/// </summary>
public sealed class CompleteDetailsRequestValidator : AbstractValidator<CompleteDetailsRequest>
{
    private const string NameTooLong = "A name cannot be longer than 200 characters.";
    private const string AddressTooLong = "An address cannot be longer than 300 characters.";

    public CompleteDetailsRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.Nic)
            .PatientNic()
            .When(request => !string.IsNullOrWhiteSpace(request.Nic));

        RuleFor(request => request.FullName)
            .Must(name => Fits(name, 200)).WithMessage(NameTooLong)
            .When(request => !string.IsNullOrWhiteSpace(request.FullName));

        RuleFor(request => request.Phone)
            .PatientPhone()
            .When(request => !string.IsNullOrWhiteSpace(request.Phone));

        RuleFor(request => request.Address)
            .Must(address => Fits(address, 300)).WithMessage(AddressTooLong)
            .When(request => !string.IsNullOrWhiteSpace(request.Address));

        RuleFor(request => request.EmergencyContactName)
            .Must(name => Fits(name, 200)).WithMessage(NameTooLong)
            .When(request => !string.IsNullOrWhiteSpace(request.EmergencyContactName));

        RuleFor(request => request.EmergencyContactPhone)
            .PatientPhone()
            .When(request => !string.IsNullOrWhiteSpace(request.EmergencyContactPhone));

        RuleFor(request => request.DateOfBirth)
            .PatientDateOfBirth()
            .MatchesNic(request => request.Nic)
            .When(request => request.DateOfBirth.HasValue);
    }

    private static bool Fits(string? value, int maxLength) => value!.Trim().Length <= maxLength;
}
