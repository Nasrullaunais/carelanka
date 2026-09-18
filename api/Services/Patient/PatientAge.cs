namespace CareLanka.Api.Services.Patient;

/// <summary>
/// Age in whole years, or null when nobody recorded a date of birth.
/// </summary>
/// <remarks>
/// Deliberately separate from <c>BedPlacementRules.IsChild</c>, which answers a different
/// question: a missing date of birth is null here and <c>false</c> there, because a screen with
/// nothing to show is not the same as a ward decision that has to go one way or the other.
/// </remarks>
public static class PatientAge
{
    public static int? InYears(DateOnly? dateOfBirth, DateOnly asOf)
    {
        if (dateOfBirth is not { } born)
        {
            return null;
        }

        var age = asOf.Year - born.Year;

        return born > asOf.AddYears(-age) ? age - 1 : age;
    }

    public static int? InYears(DateOnly? dateOfBirth)
        => InYears(dateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow));
}
