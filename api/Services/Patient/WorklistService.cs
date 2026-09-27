using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using Microsoft.EntityFrameworkCore;
using AdmissionEntity = CareLanka.Api.Data.Entities.Patient.Admission;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// Everyone physically in the hospital's care. A booking is not on it: until the patient walks
/// through the door there is nothing here to do for them, and the desk works those from the
/// expected visits screen instead.
/// </summary>
public sealed class WorklistService : IWorklistService
{
    private readonly CareLankaDbContext _db;
    private readonly IBedRegistryService _beds;

    public WorklistService(CareLankaDbContext db, IBedRegistryService beds)
    {
        _db = db;
        _beds = beds;
    }

    private static readonly AdmissionStatus[] ClosedStatuses =
    [
        AdmissionStatus.Discharged,
        AdmissionStatus.Cancelled
    ];

    public async Task<PagedResult<WorklistRow>> ListAsync(
        string? search,
        bool includeFinished,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var visits = _db.Admissions.AsNoTracking().Include(a => a.Patient);

        var query = includeFinished
            ? visits.AsQueryable()
            : visits.Where(a => !ClosedStatuses.Contains(a.Status));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";

            query = query.Where(a =>
                EF.Functions.ILike(a.Patient.FullName, pattern)
                || EF.Functions.ILike(a.Patient.PatientCode, pattern)
                || (a.Patient.Nic != null && EF.Functions.ILike(a.Patient.Nic, pattern)));
        }

        var totalItems = await query.CountAsync(ct);

        var admissions = await query
            .OrderByDescending(a => a.AdmittedAt ?? a.CreatedAt)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ids = admissions.Select(a => a.Id).ToList();

        var beds = ids.Count == 0
            ? new Dictionary<Guid, BedLabel>()
            : await BedLabels.LiveByAdmissionAsync(_db, _beds, ids, now, ct);

        return PagedResult<WorklistRow>.From(
            admissions.Select(admission => ToRow(admission, beds)).ToList(),
            page, pageSize, totalItems);
    }

    private static WorklistRow ToRow(
        AdmissionEntity admission, IReadOnlyDictionary<Guid, BedLabel> beds)
    {
        var label = beds.TryGetValue(admission.Id, out var found) ? found : (BedLabel?)null;

        return new WorklistRow
        {
            Id = admission.Id,
            Patient = new PatientSummary
            {
                Id = admission.Patient.Id,
                PatientCode = admission.Patient.PatientCode,
                FullName = admission.Patient.FullName,
                Nic = admission.Patient.Nic,
                TempReference = admission.Patient.TempReference,
                Gender = admission.Patient.Gender,
                DateOfBirth = admission.Patient.DateOfBirth
            },
            Status = StatusOf(admission.Status),
            Source = admission.Source,
            AdmissionCategory = admission.Category,
            Urgency = admission.Urgency,
            IsInfectious = admission.IsInfectious,
            WardName = label?.WardName,
            BedNumber = label?.BedNumber,
            When = admission.AdmittedAt ?? admission.CreatedAt
        };
    }

    private static WorklistStatus StatusOf(AdmissionStatus status) => status switch
    {
        AdmissionStatus.AwaitingBed => WorklistStatus.AwaitingBed,
        AdmissionStatus.AwaitingApproval => WorklistStatus.AwaitingBed,
        AdmissionStatus.BedReserved => WorklistStatus.BedReady,
        AdmissionStatus.Admitted => WorklistStatus.Admitted,
        AdmissionStatus.ReadyForDischarge => WorklistStatus.Admitted,
        AdmissionStatus.Discharged => WorklistStatus.Completed,
        AdmissionStatus.Cancelled => WorklistStatus.Cancelled,

        _ => throw new NotSupportedException($"No worklist reading for {status}.")
    };
}
