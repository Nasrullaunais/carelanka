using System.Text.RegularExpressions;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

/// <summary>
/// Every field is optional because this is a patch - blank means "leave it alone", which is how
/// the service reads it. Rules run against the trimmed value so they see exactly the string that
/// gets stored, and the NIC and phone formats are the ones registration already enforces.
/// </summary>
public sealed class CompleteDetailsRequestValidator : AbstractValidator<CompleteDetailsRequest>
{
    private static readonly Regex NicFormat =
        new(PatientIdentifierFormats.Nic, RegexOptions.Compiled);

    private static readonly Regex PhoneFormat =
        new(PatientIdentifierFormats.Phone, RegexOptions.Compiled);

    private const string NicTooLong = "A NIC cannot be longer than 20 characters.";
    private const string NameTooLong = "A name cannot be longer than 200 characters.";
    private const string PhoneTooLong = "A phone number cannot be longer than 20 characters.";
    private const string AddressTooLong = "An address cannot be longer than 300 characters.";

    public CompleteDetailsRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.Nic)
            .Must(nic => Fits(nic, 20)).WithMessage(NicTooLong)
            .Must(nic => NicFormat.IsMatch(nic!.Trim()))
            .WithMessage(PatientIdentifierFormats.NicMessage)
            .When(request => !string.IsNullOrWhiteSpace(request.Nic));

        RuleFor(request => request.FullName)
            .Must(name => Fits(name, 200)).WithMessage(NameTooLong)
            .When(request => !string.IsNullOrWhiteSpace(request.FullName));

        RuleFor(request => request.Phone)
            .Must(phone => Fits(phone, 20)).WithMessage(PhoneTooLong)
            .Must(phone => PhoneFormat.IsMatch(phone!.Trim()))
            .WithMessage(PatientIdentifierFormats.PhoneMessage)
            .When(request => !string.IsNullOrWhiteSpace(request.Phone));

        RuleFor(request => request.Address)
            .Must(address => Fits(address, 300)).WithMessage(AddressTooLong)
            .When(request => !string.IsNullOrWhiteSpace(request.Address));

        RuleFor(request => request.EmergencyContactName)
            .Must(name => Fits(name, 200)).WithMessage(NameTooLong)
            .When(request => !string.IsNullOrWhiteSpace(request.EmergencyContactName));

        RuleFor(request => request.EmergencyContactPhone)
            .Must(phone => Fits(phone, 20)).WithMessage(PhoneTooLong)
            .Must(phone => PhoneFormat.IsMatch(phone!.Trim()))
            .WithMessage(PatientIdentifierFormats.PhoneMessage)
            .When(request => !string.IsNullOrWhiteSpace(request.EmergencyContactPhone));

        RuleFor(request => request.DateOfBirth)
            .Must(dateOfBirth => dateOfBirth <= Today)
            .WithMessage("A date of birth cannot be in the future.")
            .Must(dateOfBirth => dateOfBirth >= Today.AddYears(-PatientIdentifierFormats.MaxAgeYears))
            .WithMessage(
                $"A date of birth cannot be more than {PatientIdentifierFormats.MaxAgeYears} "
                + "years ago. Check the year.")
            .When(request => request.DateOfBirth.HasValue);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static bool Fits(string? value, int maxLength) => value!.Trim().Length <= maxLength;
}
