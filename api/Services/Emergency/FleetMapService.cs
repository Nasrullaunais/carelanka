using CareLanka.Api.Data;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Emergency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareLanka.Api.Services.Emergency;

/// <summary>
/// One read for the dispatcher's map: every active ambulance and every open call, unpaged, with
/// the run that links them. A hospital fleet is tens of vehicles, so this stays small.
/// </summary>
public sealed class FleetMapService(
    CareLankaDbContext db,
    IAmbulanceEligibilityService eligibility,
    TimeProvider clock,
    IOptions<EmergencyOptions> options) : IFleetMapService
{
    private static readonly CallStatus[] OpenCallStatuses = [CallStatus.Received, CallStatus.Dispatched, CallStatus.EnRoute];

    public async Task<FleetMap> GetAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var maxAge = TimeSpan.FromMinutes(options.Value.LocationMaxAgeMinutes);

        var ambulances = await db.Ambulances.AsNoTracking()
            .Where(ambulance => ambulance.IsActive)
            .OrderBy(ambulance => ambulance.RegistrationNumber)
            .ToListAsync(cancellationToken);
        var liveRuns = await db.Dispatches.AsNoTracking()
            .Where(dispatch => DispatchStatusExtensions.LiveStatuses.Contains(dispatch.Status))
            .Select(dispatch => new { dispatch.Id, dispatch.AmbulanceId, dispatch.EmergencyCallId, dispatch.Status })
            .ToListAsync(cancellationToken);
        var runByAmbulance = liveRuns.ToDictionary(run => run.AmbulanceId);
        var runByCall = liveRuns.ToDictionary(run => run.EmergencyCallId);
        var crewCounts = await db.AmbulanceCrewAssignments.AsNoTracking()
            .Where(assignment => assignment.UnassignedAt == null)
            .GroupBy(assignment => assignment.AmbulanceId)
            .Select(group => new { AmbulanceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.AmbulanceId, item => item.Count, cancellationToken);
        var calls = await db.EmergencyCalls.AsNoTracking()
            .Where(call => OpenCallStatuses.Contains(call.Status))
            .OrderBy(call => call.Priority).ThenBy(call => call.CreatedAt)
            .Select(call => new { call.Id, call.Priority, call.Status, call.AddressLabel, call.Latitude, call.Longitude, call.CreatedAt })
            .ToListAsync(cancellationToken);

        return new FleetMap
        {
            GeneratedAt = now,
            LocationMaxAgeMinutes = options.Value.LocationMaxAgeMinutes,
            Ambulances = ambulances.Select(ambulance =>
            {
                runByAmbulance.TryGetValue(ambulance.Id, out var run);
                var crewCount = crewCounts.GetValueOrDefault(ambulance.Id);
                var decision = eligibility.Decide(new AmbulanceEligibilityFacts(
                    ambulance.IsActive, ambulance.Status, crewCount, run?.Id,
                    ambulance.CurrentLatitude.HasValue && ambulance.CurrentLongitude.HasValue,
                    ambulance.LocationUpdatedAt));
                return new FleetMapAmbulance
                {
                    Id = ambulance.Id,
                    RegistrationNumber = ambulance.RegistrationNumber,
                    Status = ambulance.Status,
                    OutOfServiceReason = ambulance.OutOfServiceReason,
                    Latitude = ambulance.CurrentLatitude,
                    Longitude = ambulance.CurrentLongitude,
                    LocationUpdatedAt = ambulance.LocationUpdatedAt,
                    LocationIsStale = ambulance.LocationUpdatedAt is not { } updatedAt || now - updatedAt > maxAge,
                    CurrentCrewCount = crewCount,
                    RequiredCrewCount = decision.RequiredCrewCount,
                    IsEligible = decision.IsEligible,
                    EligibilityBlockReasons = decision.BlockReasons,
                    ActiveDispatchId = run?.Id,
                    ActiveDispatchStatus = run?.Status,
                    ActiveCallId = run?.EmergencyCallId
                };
            }).ToList(),
            Calls = calls.Select(call => new FleetMapCall
            {
                Id = call.Id,
                Priority = call.Priority,
                Status = call.Status,
                AddressLabel = call.AddressLabel,
                Latitude = call.Latitude,
                Longitude = call.Longitude,
                WaitingMinutes = EmergencyCallClosure.WaitingMinutes(call.Status, call.CreatedAt, now),
                AssignedAmbulanceId = runByCall.TryGetValue(call.Id, out var run) ? run.AmbulanceId : null,
                CreatedAt = call.CreatedAt
            }).ToList()
        };
    }
}
