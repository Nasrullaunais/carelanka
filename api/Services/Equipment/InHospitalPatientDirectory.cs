using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Services.Equipment;

public sealed class InHospitalPatientDirectory : IInHospitalPatientDirectory
{
    // Awaiting-bed and awaiting-approval are in deliberately: a specimen is often taken before
    // anybody has found a bed. Discharged and cancelled are out.
    private static readonly AdmissionStatus[] StillHere =
    [
        AdmissionStatus.AwaitingBed,
        AdmissionStatus.AwaitingApproval,
        AdmissionStatus.BedReserved,
        AdmissionStatus.Admitted,
        AdmissionStatus.ReadyForDischarge
    ];

    private const int EveryCurrentVisit = 500;

    private readonly IAdmissionService _admissions;

    public InHospitalPatientDirectory(IAdmissionService admissions) => _admissions = admissions;

    // Matched on ward name because AdmissionSummary publishes a name and not a ward id, and
    // filtered here rather than in the query for the same reason. Honest at a few hundred beds
    // and wrong at ten thousand: when M4 publishes the wardId filter on GET /admissions that
    // STUBS.md already calls unblocked, this method becomes one delegating call.
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
        // appear twice, and it does not matter which row is picked because a report is filed
        // against the patient rather than the visit.
        return rows
            .GroupBy(row => row.PatientId)
            .Select(group => group.First())
            .OrderBy(row => row.WardName ?? string.Empty)
            .ThenBy(row => row.BedNumber ?? string.Empty)
            .ThenBy(row => row.FullName)
            .ToList();
    }
}
