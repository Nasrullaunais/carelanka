using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Staff;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Services.Staff;

public sealed class StaffReportsService : IStaffReportsService
{
    private readonly CareLankaDbContext _db;

    public StaffReportsService(CareLankaDbContext db)
    {
        _db = db;
    }

    public async Task<CoverageReport> GetCoverageReportAsync(
        CoverageReportParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.From.HasValue || !parameters.To.HasValue)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "From and To dates are required.");
        }

        if (parameters.To.Value < parameters.From.Value)
        {
            throw new BadRequestException(MessageCode.ValidationFailed, "To date must be greater than or equal to From date.");
        }

        var from = parameters.From.Value;
        var to = parameters.To.Value;

        var shiftQuery = _db.Shifts
            .AsNoTracking()
            .Include(s => s.Allocations)
            .Where(s => s.Date >= from && s.Date <= to);

        if (parameters.WardId.HasValue)
        {
            shiftQuery = shiftQuery.Where(s => s.WardId == parameters.WardId.Value);
        }

        var shifts = await shiftQuery.ToListAsync(cancellationToken);

        var wardIds = shifts.Select(s => s.WardId).Distinct().ToList();
        if (parameters.WardId.HasValue && !wardIds.Contains(parameters.WardId.Value))
        {
            wardIds.Add(parameters.WardId.Value);
        }

        var wardMap = await _db.Wards
            .AsNoTracking()
            .Where(w => wardIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => w.Name, cancellationToken);

        var grouped = shifts
            .GroupBy(s => new { s.WardId, s.Date })
            .ToList();

        var rows = new List<CoverageReportRow>(grouped.Count);

        foreach (var group in grouped)
        {
            var wardId = group.Key.WardId;
            var date = group.Key.Date;
            var wardName = wardMap.TryGetValue(wardId, out var name) ? name : string.Empty;

            var shiftsTotal = group.Count();
            var shiftsUnderstaffed = 0;
            var hoursBelowMinimum = 0.0;
            var groupConfirmed = 0;
            var groupHeadcountNeeded = 0;

            foreach (var shift in group)
            {
                var confirmedCount = shift.Allocations.Count(a => a.Status == AllocationStatus.Confirmed);
                var isUnderstaffed = confirmedCount < shift.MinimumHeadcount;

                groupConfirmed += confirmedCount;
                groupHeadcountNeeded += shift.HeadcountNeeded;

                if (isUnderstaffed)
                {
                    shiftsUnderstaffed++;
                    hoursBelowMinimum += GetShiftDurationHours(shift);
                }
            }

            var fillRate = groupHeadcountNeeded > 0
                ? Math.Clamp((double)groupConfirmed / groupHeadcountNeeded, 0.0, 1.0)
                : 0.0;

            rows.Add(new CoverageReportRow
            {
                WardId = wardId,
                WardName = wardName,
                Date = date,
                ShiftsTotal = shiftsTotal,
                ShiftsUnderstaffed = shiftsUnderstaffed,
                HoursBelowMinimum = hoursBelowMinimum,
                FillRate = fillRate
            });
        }

        var sortedRows = rows
            .OrderBy(r => r.WardName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Date)
            .ToList();

        var totalShifts = sortedRows.Sum(r => r.ShiftsTotal);
        var totalUnderstaffed = sortedRows.Sum(r => r.ShiftsUnderstaffed);
        var totalHoursBelow = sortedRows.Sum(r => r.HoursBelowMinimum);

        var allConfirmed = shifts.Sum(s => s.Allocations.Count(a => a.Status == AllocationStatus.Confirmed));
        var allHeadcountNeeded = shifts.Sum(s => s.HeadcountNeeded);
        var totalFillRate = allHeadcountNeeded > 0
            ? Math.Clamp((double)allConfirmed / allHeadcountNeeded, 0.0, 1.0)
            : 0.0;

        var totals = new CoverageReportTotals
        {
            ShiftsTotal = totalShifts,
            ShiftsUnderstaffed = totalUnderstaffed,
            HoursBelowMinimum = totalHoursBelow,
            FillRate = totalFillRate
        };

        return new CoverageReport
        {
            From = from,
            To = to,
            Rows = sortedRows,
            Totals = totals
        };
    }

    public static double GetShiftDurationHours(Shift shift)
    {
        if (shift.EndTime > shift.StartTime)
        {
            return (shift.EndTime - shift.StartTime).TotalHours;
        }

        // Overnight shift crosses midnight and ends on Date + 1 day
        return (shift.EndTime.ToTimeSpan() + TimeSpan.FromHours(24) - shift.StartTime.ToTimeSpan()).TotalHours;
    }
}
