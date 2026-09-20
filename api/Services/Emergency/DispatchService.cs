using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Emergency;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CareLanka.Api.Services.Emergency;

public sealed class DispatchService : IDispatchService
{
    private readonly CareLankaDbContext _db;
    private readonly IAmbulanceEligibilityService _eligibility;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;
    private readonly EmergencyOptions _options;

    public DispatchService(CareLankaDbContext db, IAmbulanceEligibilityService eligibility,
        ICurrentUser currentUser, TimeProvider clock, IOptions<EmergencyOptions> options)
        => (_db, _eligibility, _currentUser, _clock, _options) = (db, eligibility, currentUser, clock, options.Value);

    private static readonly Dictionary<DispatchStatus, AmbulanceStatus> ProgressProjection = new()
    {
        [DispatchStatus.EnRouteToScene] = AmbulanceStatus.EnRoute,
        [DispatchStatus.AtScene] = AmbulanceStatus.AtScene,
        [DispatchStatus.TransportingToHospital] = AmbulanceStatus.Transporting
    };

    public async Task<DispatchDetail> DispatchAsync(Guid callId, ManualDispatchRequest request, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var dispatch = await CreateCoreAsync(callId, request.AmbulanceId!.Value, ct);
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDetail(dispatch);
    }

    public async Task<DispatchDetail> GetMyActiveAsync(CancellationToken ct = default)
    {
        var dispatch = await _db.Dispatches
            .Include(x => x.Crew)
            .Include(x => x.Ambulance)
            .Include(x => x.EmergencyCall)
            .Where(x => DispatchStatusExtensions.LiveStatuses.Contains(x.Status)
                && x.Crew.Any(crew => crew.StaffMemberId == _currentUser.Id))
            .OrderByDescending(x => x.DispatchedAt)
            .FirstOrDefaultAsync(ct);
        return dispatch is null ? throw new NotFoundException("Live dispatch", _currentUser.Id) : ToDetail(dispatch);
    }

    public async Task<NavigationTarget> GetMyNavigationTargetAsync(Guid id, CancellationToken ct = default)
    {
        var dispatch = await OwnedAsync(id, ct);
        if (!dispatch.Status.IsLive()) throw new ConflictException(MessageCode.Conflict);

        var entrance = _options.HospitalEntrance;
        var (waypoint, latitude, longitude, label) = dispatch.Status == DispatchStatus.TransportingToHospital
            ? (NavigationWaypoint.HospitalEmergencyEntrance, entrance.Latitude, entrance.Longitude, entrance.Label)
            : (NavigationWaypoint.Scene, (double)dispatch.EmergencyCall.Latitude, (double)dispatch.EmergencyCall.Longitude, dispatch.EmergencyCall.AddressLabel);

        return new NavigationTarget
        {
            DispatchId = dispatch.Id,
            WaypointType = waypoint,
            DestinationLatitude = latitude,
            DestinationLongitude = longitude,
            DestinationLabel = label,
            GoogleMapsUrl = FormattableString.Invariant(
                $"https://www.google.com/maps/dir/?api=1&destination={latitude},{longitude}&travelmode=driving")
        };
    }

    public async Task<DispatchDetail> AcknowledgeAsync(Guid id, CancellationToken ct = default)
    {
        var dispatch = await OwnedAsync(id, ct);
        Move(dispatch, DispatchStatus.Acknowledged);
        dispatch.AcknowledgedAt = _clock.GetUtcNow();
        dispatch.AcknowledgedByStaffId = _currentUser.Id;
        dispatch.Ambulance.Status = AmbulanceStatus.Dispatched;
        await SaveAsync(ct);
        return ToDetail(dispatch);
    }

    public async Task<DispatchDetail> DeclineAsync(Guid id, DeclineDispatchRequest request, CancellationToken ct = default)
    {
        var dispatch = await OwnedAsync(id, ct);
        Move(dispatch, DispatchStatus.Declined);
        dispatch.DeclinedReason = request.Reason!.Trim();
        dispatch.Ambulance.Status = AmbulanceStatus.Available;
        dispatch.EmergencyCall.Status = CallStatus.Received;
        await SaveAsync(ct);
        return ToDetail(dispatch);
    }

    public async Task<DispatchDetail> ProgressAsync(Guid id, UpdateMyDispatchStatusRequest request, CancellationToken ct = default)
    {
        var dispatch = await OwnedAsync(id, ct);
        var target = request.Status!.Value;
        if (!ProgressProjection.TryGetValue(target, out var ambulanceStatus))
            throw new IllegalTransitionException("Dispatch", dispatch.Status.ToString(), target.ToString());
        Move(dispatch, target);
        dispatch.Ambulance.Status = ambulanceStatus;
        dispatch.EmergencyCall.Status = CallStatus.EnRoute;
        if (request.Latitude.HasValue)
        {
            dispatch.Ambulance.CurrentLatitude = request.Latitude.Value;
            dispatch.Ambulance.CurrentLongitude = request.Longitude!.Value;
            dispatch.Ambulance.LocationUpdatedAt = _clock.GetUtcNow();
        }
        await SaveAsync(ct);
        return ToDetail(dispatch);
    }

    public async Task<DispatchDetail> HandoverAsync(Guid id, RecordHandoverRequest request, CancellationToken ct = default)
    {
        var dispatch = await OwnedAsync(id, ct);
        Move(dispatch, DispatchStatus.HandedOver);
        dispatch.HandoverNotes = request.Notes?.Trim();
        dispatch.PatientCondition = request.PatientCondition?.Trim();
        dispatch.CompletedAt = _clock.GetUtcNow();
        dispatch.Ambulance.Status = AmbulanceStatus.Available;
        dispatch.EmergencyCall.Status = CallStatus.Completed;
        await SaveAsync(ct);
        return ToDetail(dispatch);
    }

    public async Task<DispatchDetail> CancelAsync(Guid id, CancelDispatchRequest request, CancellationToken ct = default)
    {
        var dispatch = await LoadAsync(id, ct);
        CancelCore(dispatch);
        dispatch.CancellationReason = request.Reason!.Trim();
        await SaveAsync(ct);
        return ToDetail(dispatch);
    }

    public async Task CancelForApprovedCancellationRequestAsync(Guid emergencyCallId, CancellationToken ct = default)
    {
        var dispatch = await _db.Dispatches
            .Include(x => x.Ambulance)
            .Include(x => x.EmergencyCall)
            .SingleOrDefaultAsync(x => x.EmergencyCallId == emergencyCallId && DispatchStatusExtensions.LiveStatuses.Contains(x.Status), ct)
            ?? throw new ConflictException(MessageCode.IllegalTransition);
        CancelCore(dispatch);
        await SaveAsync(ct);
    }

    public async Task<DispatchDetail> ReassignAsync(Guid id, ReassignDispatchRequest request, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var old = await LoadAsync(id, ct);
        RequirePrePickup(old, DispatchStatus.Reassigned);
        old.Status = DispatchStatus.Reassigned;
        old.ReassignmentReason = request.Reason!.Trim();
        old.CompletedAt = _clock.GetUtcNow();
        old.Ambulance.Status = AmbulanceStatus.Available;
        old.EmergencyCall.Status = CallStatus.Received;
        await SaveAsync(ct);
        var replacement = await CreateCoreAsync(old.EmergencyCallId, request.ReplacementAmbulanceId!.Value, ct);
        old.SupersededByDispatchId = replacement.Id;
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDetail(replacement);
    }

    private async Task<Dispatch> CreateCoreAsync(Guid callId, Guid ambulanceId, CancellationToken ct)
    {
        var call = await _db.EmergencyCalls.Include(x => x.Dispatches)
            .SingleOrDefaultAsync(x => x.Id == callId, ct) ?? throw new NotFoundException("Emergency call", callId);
        if (call.Status != CallStatus.Received || call.Dispatches.Any(x => x.Status.IsLive()))
            throw new ConflictException(MessageCode.CallNotAwaitingDispatch);
        var ambulance = await _db.Ambulances.Include(x => x.CrewAssignments)
            .SingleOrDefaultAsync(x => x.Id == ambulanceId, ct) ?? throw new NotFoundException("Ambulance", ambulanceId);
        var crew = ambulance.CrewAssignments.Where(x => x.UnassignedAt == null).ToList();
        var activeDispatchId = await _db.Dispatches
            .Where(x => x.AmbulanceId == ambulanceId && DispatchStatusExtensions.LiveStatuses.Contains(x.Status))
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
        var decision = _eligibility.Decide(new AmbulanceEligibilityFacts(ambulance.IsActive, ambulance.Status,
            crew.Count, activeDispatchId, ambulance.CurrentLatitude.HasValue && ambulance.CurrentLongitude.HasValue,
            ambulance.LocationUpdatedAt));
        if (!decision.IsEligible)
            throw new ConflictException(MessageCode.AmbulanceNotEligible,
                string.Join(", ", decision.BlockReasons.Select(EnumWire.ToWire)));
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

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(MessageCode.Conflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: DispatchConfiguration.ActiveAmbulanceUniqueIndex })
        {
            throw new ConflictException(MessageCode.AmbulanceHasActiveDispatch);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: DispatchConfiguration.ActiveCallUniqueIndex })
        {
            throw new ConflictException(MessageCode.CallNotAwaitingDispatch);
        }
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

    private void CancelCore(Dispatch dispatch)
    {
        RequirePrePickup(dispatch, DispatchStatus.Cancelled);
        dispatch.Status = DispatchStatus.Cancelled;
        dispatch.CompletedAt = _clock.GetUtcNow();
        dispatch.Ambulance.Status = AmbulanceStatus.Available;
        dispatch.EmergencyCall.Status = CallStatus.Received;
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

    private DispatchDetail ToDetail(Dispatch dispatch) => new()
    {
        Id = dispatch.Id, EmergencyCallId = dispatch.EmergencyCallId, AmbulanceId = dispatch.AmbulanceId,
        AmbulanceRegistration = dispatch.Ambulance.RegistrationNumber, CallPriority = dispatch.EmergencyCall.Priority,
        Status = dispatch.Status, DispatchedAt = dispatch.DispatchedAt, CompletedAt = dispatch.CompletedAt,
        AcknowledgedAt = dispatch.AcknowledgedAt, AcknowledgedByStaffId = dispatch.AcknowledgedByStaffId,
        DeclinedReason = dispatch.DeclinedReason, CancellationReason = dispatch.CancellationReason,
        ReassignmentReason = dispatch.ReassignmentReason, HandoverNotes = dispatch.HandoverNotes,
        PatientCondition = dispatch.PatientCondition, CrewCount = dispatch.Crew.Count,
        AcknowledgementOverdue = dispatch.IsAcknowledgementOverdue(_clock.GetUtcNow(), _options.AcknowledgementTimeoutSeconds),
        CrewStaffIds = dispatch.Crew.Select(x => x.StaffMemberId).ToList()
    };
}
