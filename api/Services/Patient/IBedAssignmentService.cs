using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// Which beds are free, and putting a patient in one of them by hand.
/// </summary>
/// <remarks>
/// Step 6 of docs/build/patient.md, and deliberately built before the agent. **The manual path
/// must always work** — if the only way to admit a patient were through the AI, the hospital
/// would stop the moment the AI stopped. It is also what makes a human genuinely in control
/// rather than only able to say yes or no to a machine.
///
/// The agent at step 11 does not replace any of this. It ranks the same candidate list and
/// then writes through the same hard rules and the same row lock.
/// </remarks>
public interface IBedAssignmentService
{
    /// <summary>
    /// The candidate list: Equipment Management's bed register joined with our assignments,
    /// with hold expiry applied.
    /// </summary>
    Task<PagedResult<AdmissionBed>> ListAvailabilityAsync(
        Guid? wardId,
        WardType? wardType,
        BedAvailabilityFilter availability,
        bool? needsIsolation,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Place an admission in a named bed, with a 30 minute hold. Moves the admission to
    /// <c>bed_reserved</c>.
    /// </summary>
    /// <remarks>
    /// Throws <c>NotFoundException</c> for an unknown admission, <c>ForbiddenException</c> when
    /// the bed is one only a duty manager may approve, and <c>ConflictException</c> when the
    /// bed is taken or breaks a hard rule.
    /// </remarks>
    Task<DTOs.Patient.BedAssignment> AssignManuallyAsync(
        Guid admissionId, AssignBedRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Swap the bed a patient is in for a different one, because the first was chosen by
    /// mistake. Leaves the admission's status exactly where it was.
    /// </summary>
    /// <remarks>
    /// The old assignment is closed with <c>ReleaseReason.Corrected</c>, which is what tells
    /// the bill to charge nothing for it. Every placement rule and role rule that applies to
    /// <see cref="AssignManuallyAsync"/> applies here unchanged — correcting a bed is not a
    /// way to reach a bed you were not allowed to choose in the first place.
    ///
    /// Throws <c>ConflictException</c> when the patient holds no bed (<c>cl_pat_028</c>) or is
    /// already in the bed asked for (<c>cl_pat_029</c>).
    /// </remarks>
    Task<DTOs.Patient.BedAssignment> CorrectBedAsync(
        Guid admissionId, CorrectBedRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Is anyone in this bed. Throws <c>NotFoundException</c> when Equipment's register has no
    /// live bed with that id.
    /// </summary>
    Task<BedOccupancyStatus> GetBedOccupancyAsync(
        Guid bedId, CancellationToken cancellationToken = default);
}
