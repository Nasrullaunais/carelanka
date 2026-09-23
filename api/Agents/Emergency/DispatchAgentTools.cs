using CareLanka.Api.Data;
using CareLanka.Api.Services.Emergency;
using Microsoft.EntityFrameworkCore;

namespace CareLanka.Api.Agents.Emergency;

public sealed class DispatchAgentTools : IDispatchAgentTools
{
    private readonly CareLankaDbContext _db;
    private readonly IAmbulanceEligibilityService _eligibility;
    private readonly IAmbulanceDistanceService _distance;

    public DispatchAgentTools(
        CareLankaDbContext db, IAmbulanceEligibilityService eligibility, IAmbulanceDistanceService distance)
    {
        _db = db;
        _eligibility = eligibility;
        _distance = distance;
    }

    public async Task<IReadOnlyList<EligibleAmbulanceCandidate>> ListEligibleAmbulancesAsync(
        IReadOnlyCollection<Guid> excludeAmbulanceIds, CancellationToken ct = default)
    {
        var rows = await _db.Ambulances.AsNoTracking()
            .Where(ambulance => ambulance.IsActive && !excludeAmbulanceIds.Contains(ambulance.Id))
            .Select(ambulance => new
            {
                ambulance.Id, ambulance.RegistrationNumber, ambulance.Status,
                ambulance.CurrentLatitude, ambulance.CurrentLongitude, ambulance.LocationUpdatedAt
            })
            .ToListAsync(ct);

        var activeDispatches = await _db.Dispatches.AsNoTracking()
            .Where(dispatch => DispatchStatusExtensions.LiveStatuses.Contains(dispatch.Status))
            .Select(dispatch => dispatch.AmbulanceId)
            .ToListAsync(ct);
        var busy = activeDispatches.ToHashSet();

        var crewCounts = await _db.AmbulanceCrewAssignments.AsNoTracking()
            .Where(assignment => assignment.UnassignedAt == null)
            .GroupBy(assignment => assignment.AmbulanceId)
            .Select(group => new { AmbulanceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.AmbulanceId, item => item.Count, ct);

        var candidates = new List<EligibleAmbulanceCandidate>();

        foreach (var ambulance in rows)
        {
            var decision = _eligibility.Decide(new AmbulanceEligibilityFacts(
                true,
                ambulance.Status,
                crewCounts.GetValueOrDefault(ambulance.Id),
                busy.Contains(ambulance.Id) ? ambulance.Id : null,
                ambulance.CurrentLatitude.HasValue && ambulance.CurrentLongitude.HasValue,
                ambulance.LocationUpdatedAt));

            if (decision.IsEligible && ambulance.CurrentLatitude is { } lat && ambulance.CurrentLongitude is { } lon)
            {
                candidates.Add(new EligibleAmbulanceCandidate(ambulance.Id, ambulance.RegistrationNumber, lat, lon));
            }
        }

        return candidates;
    }

    public async Task<IReadOnlyList<DivertibleDispatchCandidate>> GetActiveDispatchesAsync(CancellationToken ct = default)
        => await _db.Dispatches.AsNoTracking()
            .Include(dispatch => dispatch.Ambulance)
            .Include(dispatch => dispatch.EmergencyCall)
            .Where(dispatch => dispatch.Status == Data.Enums.DispatchStatus.Assigned
                || dispatch.Status == Data.Enums.DispatchStatus.Acknowledged
                || dispatch.Status == Data.Enums.DispatchStatus.EnRouteToScene)
            .Select(dispatch => new DivertibleDispatchCandidate(
                dispatch.Id, dispatch.AmbulanceId, dispatch.Ambulance.RegistrationNumber,
                dispatch.EmergencyCallId, dispatch.EmergencyCall.Priority, dispatch.EmergencyCall.AddressLabel,
                dispatch.Status, dispatch.DispatchedAt))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, int?>> GetRouteMinutesAsync(
        IReadOnlyCollection<EligibleAmbulanceCandidate> ambulances,
        decimal destinationLatitude,
        decimal destinationLongitude,
        CancellationToken ct = default)
    {
        if (ambulances.Count == 0)
        {
            return new Dictionary<Guid, int?>();
        }

        var measurement = await _distance.MeasureAsync(
            ambulances.Select(ambulance => new AmbulanceLocation(ambulance.Id, ambulance.Latitude, ambulance.Longitude)).ToList(),
            destinationLatitude, destinationLongitude, ct);

        return ambulances.ToDictionary(
            ambulance => ambulance.Id,
            ambulance => measurement.ByAmbulance.TryGetValue(ambulance.Id, out var travel)
                ? travel.DriveSeconds.HasValue ? travel.DriveSeconds.Value / 60 : (int?)null
                : null);
    }
}
