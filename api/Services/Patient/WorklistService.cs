using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using Microsoft.EntityFrameworkCore;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

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
        var pattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";

        var bookings = _db.Appointments
            .AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Scheduled);

        var visits = _db.Admissions.AsNoTracking();

        if (!includeFinished)
        {
            visits = visits.Where(a => !ClosedStatuses.Contains(a.Status));
        }

        if (pattern is not null)
        {
            bookings = bookings.Where(a =>
                EF.Functions.ILike(a.Patient.FullName, pattern)
                || EF.Functions.ILike(a.Patient.PatientCode, pattern)
                || (a.Patient.Nic != null && EF.Functions.ILike(a.Patient.Nic, pattern)));

            visits = visits.Where(a =>
                EF.Functions.ILike(a.Patient.FullName, pattern)
                || EF.Functions.ILike(a.Patient.PatientCode, pattern)
                || (a.Patient.Nic != null && EF.Functions.ILike(a.Patient.Nic, pattern)));
        }

        var bookingKeys = bookings.Select(a => new Key { Id = a.Id, When = a.ScheduledAt });

        var visitKeys = visits.Select(a => new Key
        {
            Id = a.Id,

            When = a.AdmittedAt ?? a.CreatedAt
        });

        var combined = bookingKeys.Concat(visitKeys);

        var totalItems = await combined.CountAsync(ct);

        var keys = await combined
            .OrderByDescending(key => key.When)
            .ThenBy(key => key.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ids = keys.Select(key => key.Id).ToList();

        var bookedRows = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var visitedRows = await _db.Admissions
            .AsNoTracking()
            .Include(a => a.Patient)
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var lines = new List<Line>(keys.Count);

        foreach (var key in keys)
        {
            if (bookedRows.TryGetValue(key.Id, out var booking))
            {
                lines.Add(new Line
                {
                    Id = booking.Id,
                    Kind = WorklistKind.Booking,
                    Patient = booking.Patient,
                    When = key.When,
                    Reason = booking.Reason
                });
            }
            else if (visitedRows.TryGetValue(key.Id, out var visit))
            {
                lines.Add(new Line
                {
                    Id = visit.Id,
                    Kind = WorklistKind.Visit,
                    Patient = visit.Patient,
                    VisitStatus = visit.Status,
                    Source = visit.Source,
                    Category = visit.Category,
                    Urgency = visit.Urgency,
                    When = key.When
                });
            }

        }

        var beds = await LabelBedsAsync(lines, now, ct);

        return PagedResult<WorklistRow>.From(
            lines.Select(line => ToRow(line, beds)).ToList(), page, pageSize, totalItems);
    }

    private async Task<IReadOnlyDictionary<Guid, BedLabel>> LabelBedsAsync(
        IReadOnlyCollection<Line> lines, DateTimeOffset now, CancellationToken ct)
    {
        var visitIds = lines
            .Where(line => line.Kind == WorklistKind.Visit)
            .Select(line => line.Id)
            .ToList();

        if (visitIds.Count == 0)
        {
            return new Dictionary<Guid, BedLabel>();
        }

        return await BedLabels.LiveByAdmissionAsync(_db, _beds, visitIds, now, ct);
    }

    private static WorklistRow ToRow(Line line, IReadOnlyDictionary<Guid, BedLabel> beds)
    {
        var label = line.Kind == WorklistKind.Visit && beds.TryGetValue(line.Id, out var found)
            ? found
            : (BedLabel?)null;

        return new WorklistRow
        {
            Id = line.Id,
            Kind = line.Kind,
            Patient = new PatientSummary
            {
                Id = line.Patient.Id,
                PatientCode = line.Patient.PatientCode,
                FullName = line.Patient.FullName,
                Nic = line.Patient.Nic,
                TempReference = line.Patient.TempReference,
                Gender = line.Patient.Gender,
                DateOfBirth = line.Patient.DateOfBirth
            },
            Status = StatusOf(line),

            RequiresBed = line.Category is { } category && BedPlacementRules.RequiresBed(category),

            Source = line.Source,
            AdmissionCategory = line.Category,
            Urgency = line.Urgency,
            WardName = label?.WardName,
            BedNumber = label?.BedNumber,
            When = line.When,
            Reason = line.Reason
        };
    }

    private static WorklistStatus StatusOf(Line line) => line.VisitStatus switch
    {
        null => WorklistStatus.NotArrived,
        AdmissionStatus.AwaitingBed => WorklistStatus.AwaitingBed,
        AdmissionStatus.AwaitingApproval => WorklistStatus.AwaitingBed,
        AdmissionStatus.BedReserved => WorklistStatus.BedReady,
        AdmissionStatus.Admitted => WorklistStatus.Admitted,
        AdmissionStatus.ReadyForDischarge => WorklistStatus.Admitted,
        AdmissionStatus.Discharged => WorklistStatus.Completed,
        AdmissionStatus.Cancelled => WorklistStatus.Cancelled,

        _ => throw new NotSupportedException($"No worklist reading for {line.VisitStatus}.")
    };

    private sealed class Line
    {
        public Guid Id { get; init; }

        public WorklistKind Kind { get; init; }

        public PatientEntity Patient { get; init; } = null!;

        public AdmissionStatus? VisitStatus { get; init; }

        public AdmissionSource? Source { get; init; }

        public AdmissionCategory? Category { get; init; }

        public AdmissionUrgency? Urgency { get; init; }

        public DateTimeOffset When { get; init; }

        public string? Reason { get; init; }
    }

    private sealed class Key
    {
        public Guid Id { get; init; }

        public DateTimeOffset When { get; init; }
    }

}
