using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using WardEntity = CareLanka.Api.Data.Entities.Patient.Ward;

namespace CareLanka.Api.Services.Patient;

public static class BedPlacementRules
{
    public static bool RequiresBed(AdmissionCategory category) => true;

    public static bool RequiresBed(AdmissionCategory? category) => true;

    public static int Rung(AdmissionCategory category) => category switch
    {
        AdmissionCategory.Icu => 0,
        _ => 2
    };

    public static int Rung(WardType wardType) => wardType switch
    {
        WardType.Icu => 0,
        WardType.Hdu => 1,
        _ => 2
    };

    public static bool IsDowngrade(AdmissionCategory category, WardType wardType)
        => Rung(wardType) > Rung(category);

    public static bool IsMoreAcuteThanNeeded(AdmissionCategory category, WardType wardType)
        => Rung(wardType) < Rung(category);

    public static bool NeedsDutyManager(AdmissionCategory category, WardType wardType)
        => IsDowngrade(category, wardType) || IsMoreAcuteThanNeeded(category, wardType);

    public const int PediatricAgeLimit = 18;

    public static int? AgeOn(DateOnly? dateOfBirth, DateOnly asOf)
    {
        if (dateOfBirth is not { } born)
        {
            return null;
        }

        var age = asOf.Year - born.Year;

        if (born > asOf.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    public static bool IsChild(DateOnly? dateOfBirth, DateOnly asOf)
        => AgeOn(dateOfBirth, asOf) is { } age && age < PediatricAgeLimit;

    public static bool EnsurePlaceable(
        AdmissionCategory category,
        Gender gender,
        DateOnly? dateOfBirth,
        bool isInfectious,
        WardEntity? ward,
        RegisteredBed bed,
        bool mayPlaceMoreAcute = false)
    {
        if (ward is null)
        {
            throw new ConflictException(MessageCode.BedWardNotInService, bed.BedNumber);
        }

        if (bed.Condition == BedCondition.OutOfService)
        {
            throw new ConflictException(MessageCode.BedOutOfService, bed.BedNumber);
        }

        if (!mayPlaceMoreAcute && IsMoreAcuteThanNeeded(category, ward.WardType))
        {
            throw new ConflictException(
                MessageCode.BedWardTooAcute,
                bed.BedNumber,
                EnumWire.ToWire(ward.WardType),
                EnumWire.ToWire(category));
        }

        if (!AcceptsGender(ward.GenderPolicy, gender))
        {
            throw new ConflictException(
                MessageCode.BedWardGenderPolicy,
                bed.BedNumber,
                EnumWire.ToWire(ward.GenderPolicy),
                EnumWire.ToWire(gender));
        }

        if (ward.WardType == WardType.Pediatric
            && !IsChild(dateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            throw new ConflictException(
                MessageCode.BedWardPediatricAdult, bed.BedNumber, PediatricAgeLimit);
        }

        if (isInfectious && !bed.HasIsolation)
        {
            throw new ConflictException(MessageCode.BedNeedsIsolation, bed.BedNumber);
        }

        return IsDowngrade(category, ward.WardType);
    }

    private static bool AcceptsGender(GenderPolicy policy, Gender gender) => policy switch
    {
        GenderPolicy.Mixed => true,
        GenderPolicy.Male => gender == Gender.Male,
        GenderPolicy.Female => gender == Gender.Female,
        _ => false
    };
}
