using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using WardEntity = CareLanka.Api.Data.Entities.Patient.Ward;

namespace CareLanka.Api.Services.Patient;

public static class BedPlacementRules
{
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

    /// <summary>
    /// Whether this kind of ward suits this care level, on top of the acuity rungs. Deliberately
    /// generous: a general ward takes any patient below ICU, and the ICU/HDU/children's wards are
    /// left to the rungs and the age rule. Only a specialist ward the patient does not belong in
    /// is outside it - and even that is the duty manager's call, not a refusal.
    /// </summary>
    public static bool FitsWard(AdmissionCategory category, WardType wardType, bool isInfectious)
        => wardType switch
        {
            WardType.Surgical => category is AdmissionCategory.Surgical or AdmissionCategory.Emergency,
            WardType.Maternity => category == AdmissionCategory.Maternity,
            WardType.Emergency => category == AdmissionCategory.Emergency,
            WardType.MentalHealth => category == AdmissionCategory.General,
            WardType.Isolation => isInfectious,
            _ => true
        };

    public static bool MayHaveCategory(AdmissionCategory category, Gender gender)
        => category != AdmissionCategory.Maternity || gender != Gender.Male;

    public static void EnsureMayHaveCategory(AdmissionCategory category, Gender gender)
    {
        if (!MayHaveCategory(category, gender))
        {
            throw new ConflictException(MessageCode.MaternityNeedsFemalePatient);
        }
    }

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
