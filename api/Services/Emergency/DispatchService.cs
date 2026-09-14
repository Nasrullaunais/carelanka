using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Emergency;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareLanka.Api.Services.Emergency;

public sealed class DispatchService : IDispatchService
{
    private readonly CareLankaDbContext _db;
    private readonly IAmbulanceEligibilityService _eligibility;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;

    public DispatchService(CareLankaDbContext db, IAmbulanceEligibilityService eligibility,
        ICurrentUser currentUser, TimeProvider clock)
        => (_db, _eligibility, _currentUser, _clock) = (db, eligibility, currentUser, clock);

    public async Task<DispatchDetail> DispatchAsync(Guid callId, ManualDispatchRequest request, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var dispatch = await CreateCoreAsync(callId, request.AmbulanceId!.Value, ct);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return ToDetail(dispatch);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: var constraint }
        && constraint is DispatchConfiguration.ActiveAmbulanceUniqueIndex or DispatchConfiguration.ActiveCallUniqueIndex)
        {
            throw new ConflictException(MessageCode.AmbulanceHasActiveDispatch);
        }
    }

    public async Task<DispatchDetail> AcknowledgeAsync(Guid id, CancellationToken ct = default)
    {
        var dispatch = await OwnedAsync(id, ct);
        Move(dispatch, DispatchStatus.Acknowledged);
        dispatch.AcknowledgedAt = _clock.GetUtcNow();
        dispatch.AcknowledgedByStaffId = _currentUser.Id;
        dispatch.Ambulance.Status = AmbulanceStatus.Dispatched;
        await _db.SaveChangesAsync(ct);
        return ToDetail(dispatch);
    }

    public async Task<DispatchDetail> DeclineAsync(Guid id, DeclineDispatchRequest request, CancellationToken ct = default)
    {
        var dispatch = await OwnedAsync(id, ct);
        Move(dispatch, DispatchStatus.Declined);
        dispatch.DeclinedReason = request.Reason!.Trim();
        dispatch.Ambulance.Status = AmbulanceStatus.Available;
        dispatch.EmergencyCall.Status = CallStatus.Received;
        await _db.SaveChangesAsync(ct);
        return ToDetail(dispatch);
    }

    public async Task<DispatchDetail> ProgressAsync(Guid id, UpdateMyDispatchStatusRequest request, CancellationToken ct = default)
    {
        var dispatch = await OwnedAsync(id, ct);
        var target = request.Status!.Value;
        Move(dispatch, target);
        dispatch.Ambulance.Status = target switch
        {
            DispatchStatus.EnRouteToScene => AmbulanceStatus.EnRoute,
            DispatchStatus.AtScene => AmbulanceStatus.AtScene,
            DispatchStatus.TransportingToHospital => AmbulanceStatus.Transporting,
            _ => throw new IllegalTransitionException("Dispatch", dispatch.Status.ToString(), target.ToString())
        };
        dispatch.EmergencyCall.Status = target == DispatchStatus.EnRouteToScene ? CallStatus.EnRoute : CallStatus.EnRoute;
        if (request.Latitude.HasValue)
        {
            dispatch.Ambulance.CurrentLatitude = request.Latitude.Value;
            dispatch.Ambulance.CurrentLongitude = request.Longitude!.Value;
            dispatch.Ambulance.LocationUpdatedAt = _clock.GetUtcNow();
        }
        await _db.SaveChangesAsync(ct);
        return ToDetail(dispatch);
    }

    public async Task<DispatchDetail> HandoverAsync(Guid id, CancellationToken ct = default)
    {
        var dispatch = await OwnedAsync(id, ct);
        Move(dispatch, DispatchStatus.HandedOver);
        dispatch.CompletedAt = _clock.GetUtcNow();
        dispatch.Ambulance.Status = AmbulanceStatus.Available;
        dispatch.EmergencyCall.Status = CallStatus.Completed;
        await _db.SaveChangesAsync(ct);
        return ToDetail(dispatch);
    }

    public async Task<DispatchDetail> CancelAsync(Guid id, CancelDispatchRequest request, CancellationToken ct = default)
    {
        var dispatch = await LoadAsync(id, ct);
        RequirePrePickup(dispatch, DispatchStatus.Cancelled);
        dispatch.Status = DispatchStatus.Cancelled;
        dispatch.CompletedAt = _clock.GetUtcNow();
        dispatch.Ambulance.Status = AmbulanceStatus.Available;
        dispatch.EmergencyCall.Status = CallStatus.Received;
        await _db.SaveChangesAsync(ct);
        return ToDetail(dispatch);
    }

    public async Task<DispatchDetail> ReassignAsync(Guid id, ReassignDispatchRequest request, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var old = await LoadAsync(id, ct);
            RequirePrePickup(old, DispatchStatus.Reassigned);
            old.Status = DispatchStatus.Reassigned;
            old.CompletedAt = _clock.GetUtcNow();
            old.Ambulance.Status = AmbulanceStatus.Available;
            old.EmergencyCall.Status = CallStatus.Received;
            var replacement = await CreateCoreAsync(old.EmergencyCallId, request.ReplacementAmbulanceId!.Value, ct);
            old.SupersededByDispatchId = replacement.Id;
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return ToDetail(replacement);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: var constraint }
        && constraint is DispatchConfiguration.ActiveAmbulanceUniqueIndex or DispatchConfiguration.ActiveCallUniqueIndex)
        {
            throw new ConflictException(MessageCode.AmbulanceHasActiveDispatch);
        }
    }

    public async Task<int> RaiseUnacknowledgedAlertsAsync(CancellationToken ct = default)
    {
        var threshold = _clock.GetUtcNow().AddSeconds(-30);
        var rows = await _db.Dispatches.Where(d => d.Status == DispatchStatus.Assigned
                && d.DispatchedAt <= threshold && d.UnacknowledgedAlertedAt == null)
            .ToListAsync(ct);
        foreach (var dispatch in rows) dispatch.UnacknowledgedAlertedAt = _clock.GetUtcNow();
        if (rows.Count > 0) await _db.SaveChangesAsync(ct);
        return rows.Count;
    }

    private async Task<Dispatch> CreateCoreAsync(Guid callId, Guid ambulanceId, CancellationToken ct)
    {
        var call = await _db.EmergencyCalls.Include(x => x.Dispatches)
            .SingleOrDefaultAsync(x => x.Id == callId, ct) ?? throw new NotFoundException("Emergency call", callId);
        if (call.Status != CallStatus.Received || call.Dispatches.Any(x => x.Status.IsLive()))
            throw new ConflictException(MessageCode.Conflict);
        var ambulance = await _db.Ambulances.Include(x => x.CrewAssignments)
            .SingleOrDefaultAsync(x => x.Id == ambulanceId, ct) ?? throw new NotFoundException("Ambulance", ambulanceId);
        var crew = ambulance.CrewAssignments.Where(x => x.UnassignedAt == null).ToList();
        var active = await _db.Dispatches.Where(x => x.AmbulanceId == ambulanceId)
            .AnyAsync(x => DispatchStatusExtensions.LiveStatuses.Contains(x.Status), ct);
        var decision = _eligibility.Decide(new AmbulanceEligibilityFacts(ambulance.IsActive, ambulance.Status,
            crew.Count, active ? Guid.Empty : null, ambulance.CurrentLatitude.HasValue && ambulance.CurrentLongitude.HasValue,
            ambulance.LocationUpdatedAt));
        if (!decision.IsEligible) throw new ConflictException(MessageCode.Conflict);
        var dispatch = new Dispatch
        {
            Id = Guid.NewGuid(), EmergencyCallId = callId, EmergencyCall = call, AmbulanceId = ambulanceId,
            Ambulance = ambulance, Status = DispatchStatus.Assigned, DispatchedAt = _clock.GetUtcNow(),
            Crew = crew.Select(x => new DispatchCrew { Id = Guid.NewGuid(), StaffMemberId = x.StaffMemberId }).ToList()
        };
        call.Status = CallStatus.Dispatched;
        ambulance.Status = AmbulanceStatus.Dispatched;
        _db.Dispatches.Add(dispatch);
        return dispatch;
    }

    private async Task<Dispatch> OwnedAsync(Guid id, CancellationToken ct)
    {
        var dispatch = await LoadAsync(id, ct);
        if (!dispatch.Crew.Any(x => x.StaffMemberId == _currentUser.Id)) throw new ForbiddenException();
        return dispatch;
    }

    private async Task<Dispatch> LoadAsync(Guid id, CancellationToken ct) => await _db.Dispatches
        .Include(x => x.Crew).Include(x => x.Ambulance).Include(x => x.EmergencyCall)
        .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Dispatch", id);

    private static void RequirePrePickup(Dispatch dispatch, DispatchStatus target)
    {
        if (!dispatch.Status.IsPrePickup()) throw new IllegalTransitionException("Dispatch", dispatch.Status.ToString(), target.ToString());
    }

    private static void Move(Dispatch dispatch, DispatchStatus target)
    {
        var legal = (dispatch.Status, target) switch
        {
            (DispatchStatus.Assigned, DispatchStatus.Acknowledged) or (DispatchStatus.Assigned, DispatchStatus.Declined) => true,
            (DispatchStatus.Acknowledged, DispatchStatus.EnRouteToScene) => true,
            (DispatchStatus.EnRouteToScene, DispatchStatus.AtScene) => true,
            (DispatchStatus.AtScene, DispatchStatus.TransportingToHospital) => true,
            (DispatchStatus.TransportingToHospital, DispatchStatus.HandedOver) => true,
            _ => false
        };
        if (!legal) throw new IllegalTransitionException("Dispatch", dispatch.Status.ToString(), target.ToString());
        dispatch.Status = target;
    }

    private static DispatchDetail ToDetail(Dispatch dispatch) => new()
    {
        Id = dispatch.Id, EmergencyCallId = dispatch.EmergencyCallId, AmbulanceId = dispatch.AmbulanceId,
        AmbulanceRegistration = dispatch.Ambulance.RegistrationNumber, CallPriority = dispatch.EmergencyCall.Priority,
        Status = dispatch.Status, DispatchedAt = dispatch.DispatchedAt, CompletedAt = dispatch.CompletedAt,
        AcknowledgedAt = dispatch.AcknowledgedAt, AcknowledgedByStaffId = dispatch.AcknowledgedByStaffId,
        DeclinedReason = dispatch.DeclinedReason, CrewCount = dispatch.Crew.Count,
        CrewStaffIds = dispatch.Crew.Select(x => x.StaffMemberId).ToList()
    };
}
