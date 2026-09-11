using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using WardEntity = CareLanka.Api.Data.Entities.Patient.Ward;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// Hard rules H1–H5 from patient-management-plan.md §8.5, in one place, as ordinary C#.
/// </summary>
/// <remarks>
/// **These are never the model's job.** The agent at step 11 proposes a bed and then this
/// same table judges the proposal, so a proposal breaking a hard rule never reaches a human
/// (§8.7 step 7). The manual endpoint runs it too — a nurse picking by hand and an agent
/// picking by ranking are checked against exactly the same list, or "the AI cannot do X" is
/// only true of the AI.
///
/// Written against the rules and not against a screen: every failure is a distinct message
/// code, because "that bed will not work" tells a nurse nothing about which other bed might.
/// </remarks>
public static class BedPlacementRules
{
    /// <summary>
    /// Whether a visit at this care level needs a bed at all. <c>outpatient</c> does not.
    /// </summary>
    /// <remarks>
    /// **H0, and it comes before every other rule here.** Somebody in for a scan, a blood test
    /// or a dressing change is seen and goes home; they never lie down in a ward. Treating
    /// them as needing a bed put every one of them into <c>awaiting_bed</c>, where the
    /// workflow has no way out — the only route to <c>admitted</c> runs through a bed being
    /// assigned, so an outpatient sat on the bed board forever and could not be finished.
    ///
    /// <c>day_case</c> is on the other side of the line on purpose. A day case is minor
    /// surgery or dialysis: they are on a bed or a trolley for hours and it is a real bed
    /// somebody else cannot have. Only <c>outpatient</c> means "seen standing up".
    ///
    /// Derived from the care level and never stored. A clinician changing the care level has
    /// to change whether a bed is needed with it, and two columns that must agree are two
    /// columns that will not.
    /// </remarks>
    public static bool RequiresBed(AdmissionCategory category)
        => category != AdmissionCategory.Outpatient;

    /// <summary>
    /// How acute a care level is, as a step on the downgrade ladder. Lower is more intensive.
    /// </summary>
    /// <remarks>
    /// <c>day_case</c> and <c>outpatient</c> sit on the same rung as <c>inpatient</c> rather
    /// than below it, because there is no ward type below <c>general</c> — an ordinary bed is
    /// the floor. It is also why a day-case patient in a general ward is a match and not a
    /// downgrade needing a duty manager's signature for nothing.
    /// </remarks>
    private static int Rung(AdmissionCategory category) => category switch
    {
        AdmissionCategory.Icu => 0,
        AdmissionCategory.Hdu => 1,
        _ => 2
    };

    /// <summary>The most intensive care a ward of this kind can give, on the same ladder.</summary>
    /// <remarks>
    /// Maternity, pediatric and isolation wards are ordinary-bed wards for this purpose. What
    /// makes them different is who they take, and that is <c>gender_policy</c> (H3) and the
    /// bed's isolation flag (H4) — not the care level.
    /// </remarks>
    private static int Rung(WardType wardType) => wardType switch
    {
        WardType.Icu => 0,
        WardType.Hdu => 1,
        _ => 2
    };

    /// <summary>
    /// Whether this ward is below the care level the admission was filed at, which is what
    /// "downgrade" means and what forces the duty manager's approval.
    /// </summary>
    public static bool IsDowngrade(AdmissionCategory category, WardType wardType)
        => Rung(wardType) > Rung(category);

    /// <summary>
    /// A ward a ward nurse may not place into on their own: intensive care, high dependency,
    /// or any step down from the care the patient was assessed as needing.
    /// </summary>
    /// <remarks>
    /// patient-management-plan.md §5.2. ICU beds are the scarcest thing in a hospital, and a
    /// downgrade is a decision about giving somebody less care than a clinician asked for.
    /// Both are the duty manager's, and neither is a property of the route — they depend on
    /// which bed was picked, so this is checked in the service, the same way check-in is.
    /// </remarks>
    public static bool NeedsDutyManager(AdmissionCategory category, WardType wardType)
        => wardType is WardType.Icu or WardType.Hdu || IsDowngrade(category, wardType);

    /// <summary>
    /// Refuses, as a 409, a bed that breaks any hard rule for this admission. Returns whether
    /// the placement counts as a downgrade, which is the one thing the caller has to record.
    /// </summary>
    /// <param name="category">The care level a clinician filed this admission at. Never ours to change.</param>
    /// <param name="gender">The patient's, as recorded. <c>unknown</c> is a real value here, not a gap.</param>
    /// <param name="isInfectious">Set by staff. Drives H4.</param>
    /// <param name="ward">The ward the bed stands in, or null when it is missing or retired (H5).</param>
    /// <param name="bed">The bed, as Equipment's register publishes it.</param>
    /// <remarks>
    /// H1's "free" half is deliberately **not** here. Whether a bed is taken is a race, not a
    /// property of the bed, and no read can settle it — the partial unique index
    /// <c>ux_bed_assignments_live_bed</c> is what does. See
    /// <see cref="BedAssignmentService"/>.
    /// </remarks>
    public static bool EnsurePlaceable(
        AdmissionCategory category,
        Gender gender,
        bool isInfectious,
        WardEntity? ward,
        RegisteredBed bed)
    {
        // H5 — the ward must be active. A retired ward is invisible to every other read we do,
        // so a bed still pointing at one is a bed nobody should be admitted into. Reported as a
        // conflict rather than a 404: the bed itself is real, it just cannot take a patient.
        if (ward is null)
        {
            throw new ConflictException(MessageCode.BedWardNotInService, bed.BedNumber);
        }

        // H1, the half a read can answer. Out of service is Equipment's fact about the frame
        // and nothing here overrides it — a bed awaiting repair is not a bed.
        if (bed.Condition == BedCondition.OutOfService)
        {
            throw new ConflictException(MessageCode.BedOutOfService, bed.BedNumber);
        }

        // H2 — the ward must match the care level, or be a step down from it. A step *up* is
        // refused: putting a routine inpatient in intensive care is not an act of generosity,
        // it is the last ICU bed spent on somebody who does not need it.
        if (Rung(ward.WardType) < Rung(category))
        {
            throw new ConflictException(
                MessageCode.BedWardTooAcute,
                bed.BedNumber,
                EnumWire.ToWire(ward.WardType),
                EnumWire.ToWire(category));
        }

        // H3 — the ward's gender policy has to accept this patient. A property of the ward,
        // applied identically to every admission: real general wards are single-sex and real
        // ICUs are open bays, so there is no emergency exception to make here.
        //
        // `other` and `unknown` reach only a mixed ward. That is the deterministic behaviour
        // Gender.Unknown exists for — an unidentified arrival must land somewhere by rule
        // rather than by a guess about which single-sex ward they belong in.
        if (!AcceptsGender(ward.GenderPolicy, gender))
        {
            throw new ConflictException(
                MessageCode.BedWardGenderPolicy,
                bed.BedNumber,
                EnumWire.ToWire(ward.GenderPolicy),
                EnumWire.ToWire(gender));
        }

        // H4 — an infectious patient needs a bed that can isolate them. The flag is on the bed
        // and not on the ward: an isolation ward is not the only place with a side room.
        if (isInfectious && !bed.HasIsolation)
        {
            throw new ConflictException(MessageCode.BedNeedsIsolation, bed.BedNumber);
        }

        return IsDowngrade(category, ward.WardType);
    }

    /// <summary>Whether a ward with this policy takes a patient of this gender.</summary>
    private static bool AcceptsGender(GenderPolicy policy, Gender gender) => policy switch
    {
        GenderPolicy.Mixed => true,
        GenderPolicy.Male => gender == Gender.Male,
        GenderPolicy.Female => gender == Gender.Female,
        _ => false
    };
}
