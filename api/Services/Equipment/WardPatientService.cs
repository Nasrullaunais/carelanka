using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.DTOs.Patient;
using CareLanka.Api.Services.Patient;

namespace CareLanka.Api.Services.Equipment;

// Reads Patient Management through their own admission service, never their tables. The same
// shape as BedOccupancyAdapter: one file in this component that knows theirs exists.
public sealed class WardPatientService : IWardPatientService
{
    // Awaiting-bed and awaiting-approval are in deliberately: a specimen is often taken, and
    // equipment often needed, before anybody has found a bed. Discharged and cancelled are out.
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

    public WardPatientService(IAdmissionService admissions) => _admissions = admissions;

    // Matched on ward name because AdmissionSummary publishes a name and not a ward id, and
    // filtered here rather than in the query for the same reason. Honest at a few hundred beds
    // and wrong at ten thousand: when M4 publishes the wardId filter on GET /admissions that
    // STUBS.md already calls unblocked, this becomes one delegating call.
    public async Task<PagedResult<WardPatient>> ListAsync(
        string? wardName, int page, int pageSize, CancellationToken cancellationToken = default)
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

        var everyone = admissions.Items
            .Where(admission => admission.Patient is not null)
            .Where(admission => wardName is null || admission.WardName == wardName)
            .Select(admission => new WardPatient
            {
                AdmissionId = admission.Id,
                PatientId = admission.Patient!.Id,
                PatientCode = admission.Patient.PatientCode,
                FullName = admission.Patient.FullName,
                WardName = admission.WardName,
                BedNumber = admission.BedNumber,
                AdmissionStatus = admission.Status
            })
            .OrderBy(row => row.WardName ?? string.Empty)
            .ThenBy(row => row.BedNumber ?? string.Empty)
            .ThenBy(row => row.FullName)
            .ToList();

        // Paged after the ward filter, not before, or rows drop off the end of every page.
        var rows = everyone.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return PagedResult<WardPatient>.From(rows, page, pageSize, everyone.Count);
    }
}
