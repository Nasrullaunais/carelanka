using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Patient;
using Microsoft.EntityFrameworkCore;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// The ward board, built from two tables in one paged query.
/// </summary>
/// <remarks>
/// **Why this exists at all.** The patients screen used to list admissions, and an admission is
/// created by arriving — so the screen could never say "not arrived" about anybody. The person
/// booked in for a scan at eleven was invisible until she walked through the door, and the desk
/// had to look at a second screen to find her. One board, one answer.
///
/// **One row per person's business, never two.** An appointment that has been checked in is
/// terminal at <c>checked_in</c> and is excluded here; its admission stands for it. So a
/// booking becomes a visit on the board rather than appearing beside it.
///
/// Reads only. Every write still goes to the service that owns the row.
/// </remarks>
public sealed class WorklistService : IWorklistService
{
    private readonly CareLankaDbContext _db;
    private readonly IBedRegistryService _beds;

    public WorklistService(CareLankaDbContext db, IBedRegistryService beds)
    {
        _db = db;
        _beds = beds;
    }

    /// <summary>A visit that has ended. Off the board unless the caller asks for the archive.</summary>
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

        // Only scheduled bookings. Cancelled and no-show are finished with, and checked_in is
        // represented by the admission it created — include it and every checked-in patient
        // would appear twice, once as themselves and once as their own past.
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
                || (a.Patient.Nic != null && EF.Functions.ILike(a.Patient.Nic, pattern)));

            visits = visits.Where(a =>
                EF.Functions.ILike(a.Patient.FullName, pattern)
                || (a.Patient.Nic != null && EF.Functions.ILike(a.Patient.Nic, pattern)));
        }

        // The union carries **only what it has to**: an id and the one time the board sorts
        // on. Everything else is fetched afterwards, for the twenty rows that survive paging.
        //
        // It started out as a full flat projection of both tables, and that fails at run time
        // with "reading as Int32 is not supported for character varying". A UNION takes each
        // column's type from the first branch, and every enum on the booking branch is a plain
        // `null` while the same column on the visit branch is a converted string — so Postgres
        // and EF ended up disagreeing about what the column even was. Two uuid-and-timestamp
        // columns cannot disagree about anything.
        var bookingKeys = bookings.Select(a => new Key { Id = a.Id, When = a.ScheduledAt });

        var visitKeys = visits.Select(a => new Key
        {
            Id = a.Id,

            // Arrival if we have it, otherwise when the record was opened. An emergency
            // admission exists before the patient arrives, and a row with no time at all
            // could not be placed in a list sorted by time.
            When = a.AdmittedAt ?? a.CreatedAt
        });

        // UNION ALL, then order and page over the whole thing. Paging each side separately and
        // stitching the two results looks simpler and is wrong: a page boundary of the combined
        // list falls in the middle of neither half, so rows get shown twice or skipped.
        var combined = bookingKeys.Concat(visitKeys);

        var totalItems = await combined.CountAsync(ct);

        var keys = await combined
            // Newest first: what changed most recently is what somebody is dealing with. Id
            // breaks ties, or two rows sharing an instant swap places between pages and one is
            // shown twice while another is never shown at all.
            .OrderByDescending(key => key.When)
            .ThenBy(key => key.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var ids = keys.Select(key => key.Id).ToList();

        // Two ordinary reads of one page's worth of ids, so Include works and the patient is
        // projected once by EF rather than field by field twice by hand.
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

        // Which table answered is what makes a row a booking or a visit — no discriminator
        // column in the union, which is one more thing the two branches cannot type
        // differently. Ids are uuids, so no id is in both.
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

            // No else. A row deleted between the union and these two reads is simply gone, and
            // a hole in one page is better than a 500 for a race nobody can act on.
        }

        var beds = await LabelBedsAsync(lines, now, ct);

        return PagedResult<WorklistRow>.From(
            lines.Select(line => ToRow(line, beds)).ToList(), page, pageSize, totalItems);
    }

    /// <summary>
    /// Where each visit on this page is, keyed by admission id. Two queries, not one per row.
    /// </summary>
    /// <remarks>
    /// Deliberately a second pass rather than a subquery inside the projection above.
    /// <see cref="BedHold.LiveOn"/> is the single definition of "that bed is still claimed" and
    /// EF can only use it as a top-level predicate. Inlining the condition into the union query
    /// would make a third copy of a rule BedHold already warns is written twice.
    /// </remarks>
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

        var live = await _db.BedAssignments
            .AsNoTracking()
            .Where(assignment => visitIds.Contains(assignment.AdmissionId))
            .Where(BedHold.LiveOn(now))
            .Select(assignment => new { assignment.AdmissionId, assignment.BedId })
            .ToListAsync(ct);

        if (live.Count == 0)
        {
            return new Dictionary<Guid, BedLabel>();
        }

        // Equipment's beds, read through the one adapter allowed to touch them.
        var registered = await _beds.ListBedsByIdAsync(
            live.Select(claim => claim.BedId).Distinct().ToList(), ct);

        var wardNames = await _db.Wards
            .AsNoTracking()
            .Where(ward => registered.Select(bed => bed.WardId).Contains(ward.Id))
            .ToDictionaryAsync(ward => ward.Id, ward => ward.Name, ct);

        var byBedId = registered.ToDictionary(
            bed => bed.Id,
            bed => new BedLabel(
                wardNames.TryGetValue(bed.WardId, out var name) ? name : string.Empty,
                bed.BedNumber));

        // At most one live assignment per admission — ux_bed_assignments_live_admission makes
        // that a database guarantee, so nothing here can actually discard one.
        var result = new Dictionary<Guid, BedLabel>();

        foreach (var claim in live)
        {
            if (byBedId.TryGetValue(claim.BedId, out var label))
            {
                result[claim.AdmissionId] = label;
            }
        }

        return result;
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
                FullName = line.Patient.FullName,
                Nic = line.Patient.Nic,
                TempReference = line.Patient.TempReference,
                Gender = line.Patient.Gender,
                DateOfBirth = line.Patient.DateOfBirth
            },
            Status = StatusOf(line),

            // False for a booking as well as for an outpatient. Nobody has chosen a care level
            // for somebody who has not arrived, so "does this need a bed" has no answer yet,
            // and a screen must not offer to assign one on a guess.
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

    /// <summary>
    /// The board's reading of a stored status. The only place the two vocabularies meet.
    /// </summary>
    /// <remarks>
    /// <c>awaiting_approval</c> reads as "awaiting bed" because from the board's point of view
    /// it is the same situation and the same thing to do about it: the patient has no bed and
    /// somebody has to see to it. <c>ready_for_discharge</c> reads as "admitted" because they
    /// are still in the bed — the flag says they could go home, not that they have.
    /// </remarks>
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

        // Unreachable today, and a throw rather than a default bucket: a status added to
        // AdmissionStatus without a reading here would otherwise appear on the board as
        // whatever the default happened to be, silently and wrongly.
        _ => throw new NotSupportedException($"No worklist reading for {line.VisitStatus}.")
    };

    /// <summary>One assembled row, before it becomes a DTO. Nullable-heavy: a booking has no care level.</summary>
    private sealed class Line
    {
        public Guid Id { get; init; }

        public WorklistKind Kind { get; init; }

        public PatientEntity Patient { get; init; } = null!;

        /// <summary>Null on a booking, which is what makes it a booking.</summary>
        public AdmissionStatus? VisitStatus { get; init; }

        public AdmissionSource? Source { get; init; }

        public AdmissionCategory? Category { get; init; }

        public AdmissionUrgency? Urgency { get; init; }

        public DateTimeOffset When { get; init; }

        public string? Reason { get; init; }
    }

    /// <summary>What the union carries: an id and the one column the board sorts on.</summary>
    private sealed class Key
    {
        public Guid Id { get; init; }

        public DateTimeOffset When { get; init; }
    }

    /// <summary>Where a bed is, for display. Equipment's bed number, our ward name.</summary>
    private readonly record struct BedLabel(string WardName, string BedNumber);
}
