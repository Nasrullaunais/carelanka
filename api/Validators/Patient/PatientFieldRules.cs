using System.Text.RegularExpressions;
using CareLanka.Api.Services.Patient;
using FluentValidation;

namespace CareLanka.Api.Validators.Patient;

/// <summary>
/// The patient identity fields as every request that carries them checks them. Each rule runs
/// against the trimmed value, which is the string the services store. Callers skip a blank
/// optional field with <c>When</c>.
/// </summary>
public static class PatientFieldRules
{
    private static readonly Regex NicFormat = new(PatientIdentifierFormats.Nic, RegexOptions.Compiled);
    private static readonly Regex PhoneFormat = new(PatientIdentifierFormats.Phone, RegexOptions.Compiled);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static IRuleBuilderOptions<T, string?> PatientNic<T>(this IRuleBuilder<T, string?> rule)
        => rule
            .Must(nic => nic!.Trim().Length <= 20).WithMessage("A NIC cannot be longer than 20 characters.")
            .Must(nic => NicFormat.IsMatch(nic!.Trim())).WithMessage(PatientIdentifierFormats.NicMessage)
            .Must(nic => SriLankanNic.YearProblem(nic!, Today) is null)
            .WithMessage((_, nic) => SriLankanNic.YearProblem(nic!, Today)!.Text);

    public static IRuleBuilderOptions<T, string?> PatientPhone<T>(this IRuleBuilder<T, string?> rule)
        => rule
            .Must(phone => phone!.Trim().Length <= 20).WithMessage("A phone number cannot be longer than 20 characters.")
            .Must(phone => PhoneFormat.IsMatch(phone!.Trim())).WithMessage(PatientIdentifierFormats.PhoneMessage);

    public static IRuleBuilderOptions<T, DateOnly?> PatientDateOfBirth<T>(this IRuleBuilder<T, DateOnly?> rule)
        => rule
            .Must(dateOfBirth => dateOfBirth <= Today)
            .WithMessage("A date of birth cannot be in the future.")
            .Must(dateOfBirth => dateOfBirth >= Today.AddYears(-PatientIdentifierFormats.MaxAgeYears))
            .WithMessage(
                $"A date of birth cannot be more than {PatientIdentifierFormats.MaxAgeYears} "
                + "years ago. Check the year.");

    /// <summary>
    /// Reported on the date of birth, not the NIC: the NIC on its own was already found valid,
    /// and it is the pair that disagrees.
    /// </summary>
    public static IRuleBuilderOptions<T, DateOnly?> MatchesNic<T>(
        this IRuleBuilder<T, DateOnly?> rule, Func<T, string?> nic)
        => rule
            .Must((request, dateOfBirth) => Problem(nic(request), dateOfBirth) is null)
            .WithMessage((request, dateOfBirth) => Problem(nic(request), dateOfBirth)!.Text);

    public static bool IsWellFormedNic(string? nic)
        => !string.IsNullOrWhiteSpace(nic)
            && nic.Trim().Length <= 20
            && NicFormat.IsMatch(nic.Trim())
            && SriLankanNic.YearProblem(nic, Today) is null;

    private static NicProblem? Problem(string? nic, DateOnly? dateOfBirth)
        => IsWellFormedNic(nic) && dateOfBirth is { } date
            ? SriLankanNic.DateOfBirthProblem(nic!, date)
            : null;
}
