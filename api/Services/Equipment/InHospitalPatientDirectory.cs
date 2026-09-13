using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Services.Equipment;

/// <summary>
/// Reads who is in the hospital from Patient Management's own admission service, and reshapes it
/// into what the laboratory screen needs.
/// </summary>
/// <remarks>
/// The same shape as <see cref="BedOccupancyAdapter"/>: one file in the consuming component that
/// knows the other component exists.
///
/// **Why the ward is matched on its name.** `AdmissionSummary` publishes `ward_name`, not a ward
/// id, so that is what there is to match on. The screen picks a ward from `GET /wards`, which
/// carries both, and sends the name. It is exact and case-sensitive, and ward names are unique in
/// the register, so this is narrower than it looks - but it is still a string where an id belongs.
/// When M4 publishes the `wardId` filter that `STUBS.md` already calls unblocked, this whole
/// method becomes one delegating call and the filtering disappears.
/// </remarks>
public sealed class InHospitalPatientDirectory : IInHospitalPatientDirectory
{
    /// <summary>
    /// A visit that is in the building or on its way to a bed. Discharged and cancelled are out:
    /// a lab filing today's result wants today's patients. Awaiting-bed and awaiting-approval are
    /// in, because a specimen is often taken before anybody has found a bed.
    /// </summary>
    private static readonly AdmissionStatus[] StillHere =
    [
        AdmissionStatus.AwaitingBed,
        AdmissionStatus.AwaitingApproval,
        AdmissionStatus.BedReserved,
        AdmissionStatus.Admitted,
        AdmissionStatus.ReadyForDischarge
    ];

    /// <summary>
    /// One page, deliberately large. The hospital has a few hundred beds, so every current visit
    /// fits in one read and the screen pages the result itself rather than paging a filter that
    /// is applied afterwards - which would drop rows off the end of every page.
    /// </summary>
    private const int EveryCurrentVisit = 500;

    private readonly IAdmissionService _admissions;

    public InHospitalPatientDirectory(IAdmissionService admissions) => _admissions = admissions;

    public async Task<IReadOnlyList<LabPatient>> ListAsync(
        string? wardName, CancellationToken cancellationToken = default)
    {
        var admissions = await _admissions.ListAsync(
            StillHere,
            category: null,
            source: null,
            detailsComplete: null,
            search: null,
            page: 1,
            pageSize: EveryCurrentVisit,
            sortBy: AdmissionSortField.AdmittedAt,
            sortDir: SortDirection.Desc,
            cancellationToken);

        var rows = admissions.Items
            .Where(admission => admission.Patient is not null)
            .Where(admission => wardName is null || admission.WardName == wardName)
            .Select(admission => new LabPatient
            {
                PatientId = admission.Patient!.Id,
                PatientCode = admission.Patient.PatientCode,
                FullName = admission.Patient.FullName,
                WardName = admission.WardName,
                BedNumber = admission.BedNumber,
                AdmissionStatus = admission.Status
            })
            .ToList();

        // One row per patient, not per visit. Somebody readmitted the same day would otherwise
        // appear twice and the lab would not know which row to file against - and it does not
        // matter, because a report is filed against the patient rather than the visit.
        return rows
            .GroupBy(row => row.PatientId)
            .Select(group => group.First())
            .OrderBy(row => row.WardName ?? string.Empty)
            .ThenBy(row => row.BedNumber ?? string.Empty)
            .ThenBy(row => row.FullName)
            .ToList();
    }
}
