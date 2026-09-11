using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// What a patient's NIC, phone number and date of birth are allowed to look like.
/// </summary>
/// <remarks>
/// The screen checks these too, so a typist is told at the field rather than by a 400. That is
/// help, not enforcement — a client is not a boundary, so the same rules are declared here on
/// the request and the server refuses whatever the browser did or did not do.
///
/// Every rule is deliberately permissive at one end: a patient with no NIC at all is registered
/// against a temp reference and always has been, so <b>none of these make a field required</b>.
/// They only say what a filled-in field has to look like.
/// </remarks>
public static class PatientIdentifierFormats
{
    /// <summary>
    /// A Sri Lankan NIC in either form, or a passport number.
    /// </summary>
    /// <remarks>
    /// Three alternatives, in the order they are common at a Sri Lankan hospital desk:
    /// <list type="bullet">
    ///   <item>the old NIC — nine digits and a V or an X, <c>199534501V</c>;</item>
    ///   <item>the new NIC — twelve digits, <c>199745600321</c>;</item>
    ///   <item>a passport — six to fifteen letters and digits, <b>with at least one letter</b>.</item>
    /// </list>
    /// That last condition is the one doing real work. Without it every mistyped NIC — eight
    /// digits, thirteen digits — would be waved through as "a passport, presumably", and the
    /// check would catch nothing at all. Requiring a letter means a digits-only value has to be
    /// a valid NIC or it is a typo.
    ///
    /// <b>The known cost.</b> <c>199534501Z</c> is an old NIC with the wrong final letter, and
    /// also a perfectly ordinary passport number in some country. No pattern can tell those
    /// apart, so it is accepted. Passport numbering is not standardised, and the group chose a
    /// wide range over registering every foreign patient against a temp reference.
    /// </remarks>
    public const string Nic = @"^(\d{9}[VvXx]|\d{12}|(?=.*[A-Za-z])[A-Za-z0-9]{6,15})$";

    public const string NicMessage =
        "Enter an NIC as nine digits and a V (199534501V) or as twelve digits (199745600321), "
        + "or a passport number. Leave it blank if they have no papers.";

    /// <summary>A Sri Lankan phone number: ten digits from a leading zero, or the +94 form.</summary>
    /// <remarks>
    /// <c>+94771234567</c> is the same number as <c>0771234567</c> and is what a patient reads
    /// off a phone that has roamed. Refusing it would teach the desk to retype numbers by hand,
    /// which is where digits get dropped.
    /// </remarks>
    public const string Phone = @"^(0\d{9}|\+94\d{9})$";

    public const string PhoneMessage =
        "A phone number is ten digits starting with 0, like 0771234567.";

    /// <summary>The oldest date of birth anyone will credibly type.</summary>
    /// <remarks>
    /// 120 rather than a round 100: the oldest verified human lived to 122, and a hospital that
    /// refuses to register a 106-year-old is a worse bug than one that accepts an implausible
    /// 119. The check is aimed at <c>1097</c> for <c>1997</c>, not at demographics.
    /// </remarks>
    public const int MaxAgeYears = 120;
}

/// <summary>
/// A date of birth that is in the past and inside <see cref="PatientIdentifierFormats.MaxAgeYears"/>.
/// </summary>
/// <remarks>
/// Its own attribute because <c>[Range]</c> does not understand <see cref="DateOnly"/> and
/// "today" is not a constant it could be given anyway. Null passes — date of birth is optional,
/// and an unidentified arrival has no date of birth to give.
/// </remarks>
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
