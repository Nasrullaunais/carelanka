using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using WardEntity = CareLanka.Api.Data.Entities.Patient.Ward;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// Hard rules H1–H6 from patient-management-plan.md §8.5, in one place, as ordinary C#.
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
    /// Whether this ward gives more intensive care than the admission was filed at — a routine
    /// inpatient placed into intensive care.
    /// </summary>
    /// <remarks>
    /// Every such ward is <c>icu</c> or <c>hdu</c>, because those are the only two ward types
    /// above the ordinary-bed rung — but the rule is the mismatch, not the ward type. An ICU
    /// ward is *not* more acute than an ICU patient needs, which is exactly why a nurse may
    /// put that patient in it. See <see cref="NeedsDutyManager"/>.
    /// </remarks>
    public static bool IsMoreAcuteThanNeeded(AdmissionCategory category, WardType wardType)
        => Rung(wardType) < Rung(category);

    /// <summary>
    /// A ward a ward nurse or reception may not place into on their own: one that does not give
    /// the level of care this patient was assessed as needing, in either direction.
    /// </summary>
    /// <remarks>
    /// patient-management-plan.md §5.2. <b>The rule is the match, and nothing else.</b> Whoever
    /// may place a patient may place them in the ward their care level points at — an ICU bed
    /// for an ICU patient is the right bed, and making a nurse find a duty manager for it slows
    /// down the most urgent admission in the hospital for no decision anybody has to make.
    ///
    /// What is genuinely a decision is putting somebody somewhere their care level does not
    /// point at: a step down, which gives them less care than a clinician asked for, or a step
    /// up, which spends a scarcer bed than they need. Those are the duty manager's.
    ///
    /// Not a property of the route — it depends on which bed was picked, so it is checked in the
    /// service, the same way check-in is.
    ///
    /// <b>Revised 2026-09-12.</b> This used to include every <c>icu</c> and <c>hdu</c> ward
    /// regardless of the patient, which meant an ICU patient could not be bedded without a duty
    /// manager even though the bed matched perfectly.
    /// </remarks>
    public static bool NeedsDutyManager(AdmissionCategory category, WardType wardType)
        => IsDowngrade(category, wardType) || IsMoreAcuteThanNeeded(category, wardType);

    /// <summary>The age from which a patient is an adult, and so no longer a pediatric case.</summary>
    public const int PediatricAgeLimit = 18;

    /// <summary>
    /// Whether the patient is under <see cref="PediatricAgeLimit"/> on this date. An unrecorded
    /// date of birth is <b>not</b> a child.
    /// </summary>
    /// <remarks>
    /// Unknown reads as adult on purpose, the same way <c>Gender.Unknown</c> reaches only a
    /// mixed ward: the children's ward is the narrower place to put somebody, so it takes a
    /// recorded fact to earn it rather than the absence of one. An unidentified arrival who
    /// turns out to be a child gets their date of birth filled in, and the ward appears.
    /// </remarks>
    public static bool IsChild(DateOnly? dateOfBirth, DateOnly asOf)
    {
        if (dateOfBirth is not { } born)
        {
            return false;
        }

        var age = asOf.Year - born.Year;

        // Their birthday has not come round yet this year, so they are a year younger than the
        // subtraction says.
        if (born > asOf.AddYears(-age))
        {
            age--;
        }

        return age < PediatricAgeLimit;
    }

    /// <summary>
    /// Refuses, as a 409, a bed that breaks any hard rule for this admission. Returns whether
    /// the placement counts as a downgrade, which is the one thing the caller has to record.
    /// </summary>
    /// <param name="category">The care level a clinician filed this admission at. Never ours to change.</param>
    /// <param name="gender">The patient's, as recorded. <c>unknown</c> is a real value here, not a gap.</param>
    /// <param name="dateOfBirth">The patient's, or null when nobody has recorded one. Drives H6.</param>
    /// <param name="isInfectious">Set by staff. Drives H4.</param>
    /// <param name="ward">The ward the bed stands in, or null when it is missing or retired (H5).</param>
    /// <param name="bed">The bed, as Equipment's register publishes it.</param>
    /// <param name="mayPlaceMoreAcute">
    /// Whether the caller may overrule H2 upward — the duty manager, and nobody else. Defaults
    /// to false, so the agent's proposals are judged without the exception.
    /// </param>
    /// <remarks>
    /// H1's "free" half is deliberately **not** here. Whether a bed is taken is a race, not a
    /// property of the bed, and no read can settle it — the partial unique index
    /// <c>ux_bed_assignments_live_bed</c> is what does. See
    /// <see cref="BedAssignmentService"/>.
    /// </remarks>
    public static bool EnsurePlaceable(
        AdmissionCategory category,
        Gender gender,
        DateOnly? dateOfBirth,
        bool isInfectious,
        WardEntity? ward,
        RegisteredBed bed,
        bool mayPlaceMoreAcute = false)
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
        //
        // The duty manager may overrule it, because the person who owns the consequence of an
        // empty intensive-care bed is the person who should be able to spend one. Nobody else
        // reaches this line — every more-acute ward is icu or hdu, and EnsureMayApprove has
        // already refused those to everyone but the duty manager.
        if (!mayPlaceMoreAcute && IsMoreAcuteThanNeeded(category, ward.WardType))
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

        // H6 — a children's ward takes children. Like the gender policy above it this is a
        // property of the ward and not a judgement call, so the duty manager has no exception
        // to make: an adult in a pediatric bay is wrong however full the hospital is.
        //
        // One-directional. A child may go in any ward the other rules allow — a 6-year-old
        // needing intensive care goes to intensive care — it is only the pediatric ward that
        // is closed to adults.
        if (ward.WardType == WardType.Pediatric
            && !IsChild(dateOfBirth, DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            throw new ConflictException(
                MessageCode.BedWardPediatricAdult, bed.BedNumber, PediatricAgeLimit);
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
