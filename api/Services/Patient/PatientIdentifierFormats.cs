using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.Services.Patient;

public static class PatientIdentifierFormats
{
    public const string Nic = @"^(\d{9}[VvXx]|\d{12}|(?=.*[A-Za-z])[A-Za-z0-9]{6,15})$";

    public const string NicMessage =
        "Enter an NIC as nine digits and a V (199534501V) or as twelve digits (199745600321), "
        + "or a passport number. Leave it blank if they have no papers.";

    public const string Phone = @"^(0\d{9}|\+94\d{9})$";

    public const string PhoneMessage =
        "A phone number is ten digits starting with 0, like 0771234567.";

    public const int MaxAgeYears = 120;
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class DateOfBirthAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not DateOnly dateOfBirth)
        {
            return ValidationResult.Success;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (dateOfBirth > today)
        {
            return new ValidationResult(
                "A date of birth cannot be in the future.", [context.MemberName!]);
        }

        if (dateOfBirth < today.AddYears(-PatientIdentifierFormats.MaxAgeYears))
        {
            return new ValidationResult(
                $"A date of birth cannot be more than {PatientIdentifierFormats.MaxAgeYears} "
                + "years ago. Check the year.",
                [context.MemberName!]);
        }

        return ValidationResult.Success;
    }
}
