using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Emergency;
using CareLanka.Api.Data.Entities.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using RouteLogView = CareLanka.Api.DTOs.Emergency.RouteLog;

namespace CareLanka.Api.Services.Emergency;

public sealed class DispatchService : IDispatchService
{
    private readonly CareLankaDbContext _db;
    private readonly IAmbulanceEligibilityService _eligibility;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _clock;
    private readonly EmergencyOptions _options;
    private readonly ISceneLookupQueue _sceneLookups;
    private readonly INotifier _notifier;
    private readonly IDispatchProposalLifecycle _proposals;

    public DispatchService(CareLankaDbContext db, IAmbulanceEligibilityService eligibility,
        ICurrentUser currentUser, TimeProvider clock, IOptions<EmergencyOptions> options, ISceneLookupQueue sceneLookups,
        INotifier notifier, IDispatchProposalLifecycle proposals)
        => (_db, _eligibility, _currentUser, _clock, _options, _sceneLookups, _notifier, _proposals) = (db, eligibility, currentUser, clock, options.Value, sceneLookups, notifier, proposals);

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
        await _proposals.WithdrawOpenAsync(callId, DispatchWithdrawalReason.DispatchedManually, ct);
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);
        QueueRoutePlan(dispatch);
        return ToDetail(dispatch);
    }

    public async Task<DispatchDetail> DispatchFromProposalAsync(
        Guid callId, Guid ambulanceId, Guid proposalId, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var dispatch = await CreateCoreAsync(callId, ambulanceId, ct);
        dispatch.DispatchProposalId = proposalId;
        await _proposals.MarkExecutedAsync(proposalId, dispatch.Id, null, ct);
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);
        QueueRoutePlan(dispatch);
        return ToDetail(dispatch);
    }

    public async Task<DispatchDetail> ApplyDiversionAsync(
        Guid sourceDispatchId, Guid newCallId, Guid replacementAmbulanceId, Guid proposalId, string? reason, CancellationToken ct = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var sourceCallId = await _db.Dispatches.Where(x => x.Id == sourceDispatchId).Select(x => (Guid?)x.EmergencyCallId).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Dispatch", sourceDispatchId);
        // Same order every time, so two diversions between the same calls cannot deadlock.
        foreach (var callId in new[] { sourceCallId, newCallId }.Order()) await _proposals.LockCallAsync(callId, ct);
        var source = await LoadAsync(sourceDispatchId, ct);
        RequirePrePickup(source, DispatchStatus.Reassigned);
        source.Status = DispatchStatus.Reassigned;
        source.ReassignmentReason = string.IsNullOrWhiteSpace(reason)
            ? "Diverted to a more urgent call" : reason.Trim();
        source.CompletedAt = _clock.GetUtcNow();
        source.Ambulance.Status = AmbulanceStatus.Available;
        source.EmergencyCall.Status = CallStatus.Received;
        await SaveAsync(ct);
        var replacement = await CreateCoreAsync(newCallId, replacementAmbulanceId, ct);
        replacement.DispatchProposalId = proposalId;
        source.SupersededByDispatchId = replacement.Id;
        await _proposals.MarkExecutedAsync(proposalId, replacement.Id, reason, ct);
        var sourceRecommendation = await ReopenAsync(source, excludeOwnAmbulance: false, ct);
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);
        _proposals.Wake(sourceRecommendation);
        QueueRoutePlan(replacement);
        return ToDetail(replacement);
    }

    public async Task<RouteLogView> GetRouteAsync(Guid id, CancellationToken ct = default)
    {
        var dispatch = await _db.Dispatches.AsNoTracking()
            .Include(x => x.Crew)
            .Include(x => x.RouteLog)
            .SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Dispatch", id);
        if (_currentUser.Role == PrincipalRole.AmbulanceCrew && !dispatch.Crew.Any(x => x.StaffMemberId == _currentUser.Id))
            throw new ForbiddenException();

        var route = dispatch.RouteLog ?? throw new NotFoundException("Route", id);
        return new RouteLogView
        {
            DispatchId = route.DispatchId,
            OriginLatitude = route.OriginLatitude,
            OriginLongitude = route.OriginLongitude,
            DestinationLatitude = route.DestinationLatitude,
            DestinationLongitude = route.DestinationLongitude,
            PlannedDistanceKm = (double)route.PlannedDistanceKm,
            PlannedDurationMinutes = route.PlannedDurationMinutes,
            DepartedAt = route.DepartedAt,
            ArrivedAt = route.ArrivedAt,
            MapsApiReference = route.MapsApiReference
        };
    }

    private void QueueRoutePlan(Dispatch dispatch)
    {
        if (dispatch.Ambulance.CurrentLatitude is { } latitude && dispatch.Ambulance.CurrentLongitude is { } longitude)
            _sceneLookups.Enqueue(new RoutePlanJob(dispatch.Id, latitude, longitude));
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

    public async Task<PagedResult<DispatchSummary>> ListMyHistoryAsync(MyDispatchHistoryRequest request, CancellationToken ct = default)
    {
        var query = _db.Dispatches.AsNoTracking()
            .Where(x => !DispatchStatusExtensions.LiveStatuses.Contains(x.Status)
                && x.Crew.Any(crew => crew.StaffMemberId == _currentUser.Id));

        if (request.From is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(x => x.DispatchedAt >= start);
        }

        if (request.To is { } to)
        {
            var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(x => x.DispatchedAt < end);
        }

        var totalItems = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.DispatchedAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new DispatchSummary
            {
                Id = x.Id,
                EmergencyCallId = x.EmergencyCallId,
                AmbulanceRegistration = x.Ambulance.RegistrationNumber,
                CallPriority = x.EmergencyCall.Priority,
                Status = x.Status,
                CrewCount = x.Crew.Count,
                DispatchedAt = x.DispatchedAt,
                CompletedAt = x.CompletedAt
            })
            .ToListAsync(ct);
        return PagedResult<DispatchSummary>.From(items, request.Page, request.PageSize, totalItems);
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
                $"https://www.google.com/maps/dir/?api=1&destination={latitude},{longitude}&travelmode=driving&dir_action=navigate")
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
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var dispatch = await OwnedAsync(id, ct);
        await _proposals.LockCallAsync(dispatch.EmergencyCallId, ct);
        Move(dispatch, DispatchStatus.Declined);
        dispatch.DeclinedReason = request.Reason!.Trim();
        dispatch.Ambulance.Status = AmbulanceStatus.Available;
        dispatch.EmergencyCall.Status = CallStatus.Received;
        var recommendation = await ReopenAsync(dispatch, excludeOwnAmbulance: true, ct);
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);
        _proposals.Wake(recommendation);
        return ToDetail(dispatch);
    }

    public async Task<DispatchDetail> ProgressAsync(Guid id, UpdateMyDispatchStatusRequest request, CancellationToken ct = default)
    {
        var dispatch = await OwnedAsync(id, ct);
        var target = request.Status!.Value;
        if (!ProgressProjection.TryGetValue(target, out var ambulanceStatus))
            throw new IllegalTransitionException("Dispatch", dispatch.Status.ToString(), target.ToString());
        Move(dispatch, target);
        if (dispatch.RouteLog is not null)
        {
            if (target == DispatchStatus.EnRouteToScene)
                dispatch.RouteLog.DepartedAt ??= _clock.GetUtcNow();
            if (target == DispatchStatus.AtScene)
                dispatch.RouteLog.ArrivedAt ??= _clock.GetUtcNow();
        }
        dispatch.Ambulance.Status = ambulanceStatus;
        dispatch.EmergencyCall.Status = CallStatus.EnRoute;
        if (request.Latitude.HasValue)
        {
            dispatch.Ambulance.CurrentLatitude = request.Latitude.Value;
            dispatch.Ambulance.CurrentLongitude = request.Longitude!.Value;
            dispatch.Ambulance.LocationUpdatedAt = _clock.GetUtcNow();
        }

        if (dispatch.EmergencyCall.PatientId is { } patientId)
        {
            if (target == DispatchStatus.EnRouteToScene)
            {
                await _notifier.NotifyAsync(NotificationType.AmbulanceOnTheWay, Recipients.Patient(patientId),
                    new NotificationSubject("dispatch", dispatch.Id), ct);
            }
            else if (target == DispatchStatus.AtScene)
            {
                await _notifier.NotifyAsync(NotificationType.AmbulanceArrived, Recipients.Patient(patientId),
                    new NotificationSubject("dispatch", dispatch.Id), ct);
            }
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
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var dispatch = await LoadAsync(id, ct);
        await _proposals.LockCallAsync(dispatch.EmergencyCallId, ct);
        CancelCore(dispatch);
        dispatch.CancellationReason = request.Reason!.Trim();
        var recommendation = await ReopenAsync(dispatch, excludeOwnAmbulance: true, ct);
        await SaveAsync(ct);
        await transaction.CommitAsync(ct);
        _proposals.Wake(recommendation);
        return ToDetail(dispatch);
    }

    public async Task CancelForApprovedCancellationRequestAsync(Guid emergencyCallId, CancellationToken ct = default)
    {
        var dispatch = await _db.Dispatches
            .Include(x => x.Ambulance)
            .Include(x => x.EmergencyCall)
            .SingleOrDefaultAsync(x => x.EmergencyCallId == emergencyCallId && DispatchStatusExtensions.LiveStatuses.Contains(x.Status), ct)
            ?? throw new ConflictException(MessageCode.CallHasNoLiveDispatch);
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
        QueueRoutePlan(replacement);
        return ToDetail(replacement);
    }

    private async Task<Dispatch> CreateCoreAsync(Guid callId, Guid ambulanceId, CancellationToken ct)
    {
        await _proposals.LockCallAsync(callId, ct);
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
        if (!await _db.PreAdmissionNotices.AnyAsync(x => x.EmergencyCallId == callId, ct))
        {
            _db.PreAdmissionNotices.Add(new PreAdmissionNotice
            {
                Id = Guid.NewGuid(), EmergencyCallId = callId, DispatchId = dispatch.Id,
                Status = PreAdmissionStatus.Queued, NextAttemptAt = dispatch.DispatchedAt
            });
        }

        foreach (var crewMember in dispatch.Crew)
        {
            await _notifier.NotifyAsync(NotificationType.DispatchAssigned, Recipients.Staff(crewMember.StaffMemberId),
                new NotificationSubject("dispatch", dispatch.Id), ct);
        }

        return dispatch;
    }

    private async Task<DispatchProposal> ReopenAsync(Dispatch returned, bool excludeOwnAmbulance, CancellationToken ct)
    {
        // A call dispatched before withdrawals existed can still hold an open one.
        if (await _proposals.WithdrawOpenAsync(returned.EmergencyCallId, DispatchWithdrawalReason.CallChanged, ct) is not null)
            await SaveAsync(ct);
        var excluded = (await _proposals.CarriedExclusionsAsync(returned.EmergencyCallId, ct)).ToList();
        if (excludeOwnAmbulance) excluded.Add(returned.AmbulanceId);
        return _proposals.Open(returned.EmergencyCallId, returned.EmergencyCall.Priority, allowDiversion: true, excluded);
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
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: DispatchProposalConfiguration.OpenPerCallUniqueIndex })
        {
            throw new ConflictException(MessageCode.DispatchProposalConflict);
        }
    }

    private async Task<Dispatch> OwnedAsync(Guid id, CancellationToken ct)
    {
        var dispatch = await LoadAsync(id, ct);
        if (!dispatch.Crew.Any(x => x.StaffMemberId == _currentUser.Id)) throw new ForbiddenException();
        return dispatch;
    }

    private async Task<Dispatch> LoadAsync(Guid id, CancellationToken ct) => await _db.Dispatches
        .Include(x => x.Crew).Include(x => x.Ambulance).Include(x => x.EmergencyCall).Include(x => x.RouteLog)
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

    private DispatchDetail ToDetail(Dispatch dispatch)
    {
        var call = dispatch.EmergencyCall;
        var showsCaller = _currentUser.Role != PrincipalRole.AmbulanceCrew || dispatch.Status.IsLive();
        return new DispatchDetail
        {
            Id = dispatch.Id, EmergencyCallId = dispatch.EmergencyCallId, AmbulanceId = dispatch.AmbulanceId,
            AmbulanceRegistration = dispatch.Ambulance.RegistrationNumber, CallPriority = call.Priority,
            Status = dispatch.Status, DispatchedAt = dispatch.DispatchedAt, CompletedAt = dispatch.CompletedAt,
            AcknowledgedAt = dispatch.AcknowledgedAt, AcknowledgedByStaffId = dispatch.AcknowledgedByStaffId,
            DeclinedReason = dispatch.DeclinedReason, CancellationReason = dispatch.CancellationReason,
            ReassignmentReason = dispatch.ReassignmentReason, HandoverNotes = dispatch.HandoverNotes,
            PatientCondition = dispatch.PatientCondition, SceneAddressLabel = call.AddressLabel,
            SceneDetails = showsCaller ? call.Details : null,
            SceneLatitude = call.Latitude, SceneLongitude = call.Longitude,
            SceneLocationAccuracyMetres = call.LocationAccuracyMetres,
            CallerName = showsCaller ? call.CallerName : null,
            CallerPhone = showsCaller ? call.CallerPhone : null,
            PatientIsCaller = call.PatientIsCaller,
            CrewCount = dispatch.Crew.Count,
            AcknowledgementOverdue = dispatch.IsAcknowledgementOverdue(_clock.GetUtcNow(), _options.AcknowledgementTimeoutSeconds),
            CrewStaffIds = dispatch.Crew.Select(x => x.StaffMemberId).ToList()
        };
    }
}
