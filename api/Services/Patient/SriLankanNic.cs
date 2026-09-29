using System.Text.RegularExpressions;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Services.Patient;

public sealed record NicProblem(MessageCode Code, params object?[] Args)
{
    public string Text => Code.ToText(Args);
}

/// <summary>
/// The birth year a Sri Lankan NIC carries in its leading digits. The nine-digit form gives the
/// last two digits of a 19xx year and was never issued to anyone born from 2000 on; the
/// twelve-digit form gives the full year and is held by everyone born from 2000 on, and by many
/// born earlier who have renewed. A passport number carries no year, so every check skips it.
/// </summary>
public static class SriLankanNic
{
    public const int TwelveDigitsOnlyFromYear = 2000;

    private static readonly Regex OldFormat = new(@"^(\d{2})\d{7}[VvXx]$", RegexOptions.Compiled);
    private static readonly Regex NewFormat = new(@"^(\d{4})\d{8}$", RegexOptions.Compiled);

    public static int? BirthYear(string nic)
    {
        var text = nic.Trim();

        var old = OldFormat.Match(text);
        if (old.Success)
        {
            return 1900 + int.Parse(old.Groups[1].Value);
        }

        var current = NewFormat.Match(text);
        return current.Success ? int.Parse(current.Groups[1].Value) : null;
    }

    public static NicProblem? YearProblem(string nic, DateOnly today)
    {
        if (BirthYear(nic) is not { } year)
        {
            return null;
        }

        var possible = year <= today.Year && year >= today.Year - PatientIdentifierFormats.MaxAgeYears;
        return possible ? null : new NicProblem(MessageCode.NicBirthYearImpossible, year);
    }

    public static NicProblem? DateOfBirthProblem(string nic, DateOnly dateOfBirth)
    {
        if (BirthYear(nic) is not { } year)
        {
            return null;
        }

        if (OldFormat.IsMatch(nic.Trim()) && dateOfBirth.Year >= TwelveDigitsOnlyFromYear)
        {
            return new NicProblem(MessageCode.NicOldFormatAfter2000);
        }

        return year == dateOfBirth.Year
            ? null
            : new NicProblem(MessageCode.NicBirthYearMismatch, year, dateOfBirth.Year);
    }
}
