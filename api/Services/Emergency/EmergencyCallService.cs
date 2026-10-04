using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Patient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;
using DispatchProposal = CareLanka.Api.Data.Entities.Emergency.DispatchProposal;
using EmergencyCallEntity = CareLanka.Api.Data.Entities.Emergency.EmergencyCall;

namespace CareLanka.Api.Services.Emergency;

public sealed class EmergencyCallService : IEmergencyCallService
{
    private static readonly TimeSpan ArrivalEstimateLifetime = TimeSpan.FromMinutes(2);

    private readonly CareLankaDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly EmergencyOptions _options;
    private readonly IDispatchService _dispatches;
    private readonly ISceneLookupQueue _sceneLookups;
    private readonly INotifier _notifier;
    private readonly IDispatchProposalLifecycle _lifecycle;
    private readonly IDispatchProposalService _proposals;
    private readonly IEmergencyAlerts _alerts;
    private readonly IPatientService _patients;
    private readonly IAmbulanceDistanceService _distances;
    private readonly IMemoryCache _cache;

    public EmergencyCallService(
        CareLankaDbContext db,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        IOptions<EmergencyOptions> options,
        IDispatchService dispatches,
        ISceneLookupQueue sceneLookups,
        INotifier notifier,
        IDispatchProposalLifecycle lifecycle,
        IDispatchProposalService proposals,
        IEmergencyAlerts alerts,
        IPatientService patients,
        IAmbulanceDistanceService distances,
        IMemoryCache cache)
    {
        _db = db;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _options = options.Value;
        _dispatches = dispatches;
        _sceneLookups = sceneLookups;
        _notifier = notifier;
        _lifecycle = lifecycle;
        _proposals = proposals;
        _alerts = alerts;
        _patients = patients;
        _distances = distances;
        _cache = cache;
    }

    public async Task<EmergencyCallDetail> CreateAsync(
        CreateEmergencyCallRequest request,
        CancellationToken cancellationToken = default)
    {
        var principalType = _currentUser.PrincipalType;
        var principalId = _currentUser.Id;
        var callerUserId = principalType == PrincipalType.Patient ? principalId : (Guid?)null;
        var key = request.IdempotencyKey!.Value;
        var existing = await _db.EmergencyCalls
            .AsNoTracking()
            .FirstOrDefaultAsync(call => call.IdempotencyKey == key, cancellationToken);

        if (existing is not null)
        {
            EnsureSameCaller(existing, principalType, principalId);
            return await DetailAsync(existing.Id, cancellationToken);
        }

        if (request.Priority.HasValue && _currentUser.Role != PrincipalRole.DutyManager)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }

        // A patient account calling is the account holder, whoever the ambulance is for.
        var account = principalType == PrincipalType.Patient
            ? await _patients.FindByUserAccountIdAsync(principalId, cancellationToken)
            : null;
        var patientId = request.PatientId;
        if (principalType == PrincipalType.Patient && request.PatientIsCaller!.Value)
        {
            if (patientId is not null && patientId != account?.Id)
            {
                throw new ForbiddenException(MessageCode.Forbidden);
            }

            patientId = account?.Id;
        }

        var call = new EmergencyCallEntity
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            CallerUserId = callerUserId,
            PatientIsCaller = request.PatientIsCaller!.Value,
            CallerName = Clean(request.CallerName) ?? account?.FullName,
            CallerPhone = Clean(request.CallerPhone) ?? Clean(account?.Phone),
            Latitude = request.Latitude!.Value,
            Longitude = request.Longitude!.Value,
            LocationAccuracyMetres = request.LocationAccuracyMetres!.Value,
            LocationCapturedAt = request.LocationCapturedAt!.Value,
            IdempotencyKey = key,
            Details = Clean(request.Details),
            Priority = request.Priority ?? CallPriority.High,
            Status = CallStatus.Received
        };

        _db.EmergencyCalls.Add(call);

        // No ward is known yet at intake, so this is hospital-wide - every duty manager, not one ward's.
        await _notifier.NotifyAsync(NotificationType.EmergencyCallReceived, Recipients.Role(StaffRole.DutyManager),
            new NotificationSubject("emergency_call", call.Id), cancellationToken);
        var recommendation = _lifecycle.Open(call.Id, call.Priority, allowDiversion: true, []);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: EmergencyCallConfiguration.IdempotencyKeyUniqueIndex
        })
        {
            _db.ChangeTracker.Clear();
            existing = await _db.EmergencyCalls
                .AsNoTracking()
                .SingleAsync(stored => stored.IdempotencyKey == key, cancellationToken);
            EnsureSameCaller(existing, principalType, principalId);
            return await DetailAsync(existing.Id, cancellationToken);
        }

        _lifecycle.Wake(recommendation);
        _sceneLookups.Enqueue(new AddressLookupJob(call.Id));
        return await DetailAsync(call.Id, cancellationToken);
    }

    public async Task<PagedResult<EmergencyCallSummary>> ListAsync(
        EmergencyCallListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _db.EmergencyCalls.AsNoTracking();

        if (request.Status is { } status)
        {
            query = query.Where(call => call.Status == status);
        }

        if (request.Priority is { } priority)
        {
            query = query.Where(call => call.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = SearchPattern.Contains(request.Search.Trim());
            query = query.Where(call =>
                (call.CallerName != null && EF.Functions.ILike(call.CallerName, pattern, SearchPattern.Escape))
                || (call.CallerPhone != null && EF.Functions.ILike(call.CallerPhone, pattern, SearchPattern.Escape))
                || (call.Details != null && EF.Functions.ILike(call.Details, pattern, SearchPattern.Escape)));
        }

        if (request.From is { } from)
        {
            var start = HospitalDays.StartOf(from);
            query = query.Where(call => call.CreatedAt >= start);
        }

        if (request.To is { } to)
        {
            var end = HospitalDays.EndOf(to);
            query = query.Where(call => call.CreatedAt < end);
        }

        if (request.UnassignedOnly)
        {
            query = query.Where(call => !call.Dispatches.Any(dispatch =>
                DispatchStatusExtensions.LiveStatuses.Contains(dispatch.Status)));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        query = Order(query, request.SortBy, request.SortDir == "asc");
        var rows = await query.Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(call => new
            {
                Call = call,
                ActiveDispatchId = call.Dispatches
                    .Where(dispatch => DispatchStatusExtensions.LiveStatuses.Contains(dispatch.Status))
                    .Select(dispatch => (Guid?)dispatch.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var latest = await _proposals.LatestByCallAsync(rows.Select(row => row.Call.Id).ToList(), cancellationToken);
        var items = rows.Select(row => ToSummary(row.Call, row.ActiveDispatchId, latest.GetValueOrDefault(row.Call.Id), now)).ToList();
        return PagedResult<EmergencyCallSummary>.From(items, request.Page, request.PageSize, totalItems);
    }

    public async Task<PagedResult<MyEmergencyCallSummary>> ListMineAsync(
        MyEmergencyCallListRequest request,
        CancellationToken cancellationToken = default)
    {
        var callerId = PatientCallerId();
        var query = _db.EmergencyCalls.AsNoTracking().Where(call => call.CallerUserId == callerId);

        if (request.Status is { } status)
        {
            query = query.Where(call => call.Status == status);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(call => call.CreatedAt).ThenBy(call => call.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(call => new MyEmergencyCallSummary
            {
                Id = call.Id,
                PatientIsCaller = call.PatientIsCaller,
                Priority = call.Priority,
                Status = call.Status,
                CancellationRequestStatus = call.CancellationRequestStatus,
                CreatedAt = call.CreatedAt
            })
            .ToListAsync(cancellationToken);
        return PagedResult<MyEmergencyCallSummary>.From(items, request.Page, request.PageSize, totalItems);
    }

    public async Task<EmergencyCallDetail> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var call = await _db.EmergencyCalls.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("Emergency call", id);

        if (_currentUser.PrincipalType == PrincipalType.Patient && call.CallerUserId != _currentUser.Id)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }

        if (_currentUser.PrincipalType == PrincipalType.Staff)
        {
            var allowed = _currentUser.Role switch
            {
                PrincipalRole.DutyManager => true,
                PrincipalRole.AmbulanceCrew => await _db.DispatchCrew.AnyAsync(crew =>
                    crew.StaffMemberId == _currentUser.Id && crew.Dispatch.EmergencyCallId == id, cancellationToken),
                _ => false
            };
            if (!allowed)
            {
                throw new ForbiddenException(MessageCode.Forbidden);
            }
        }

        return await DetailAsync(id, cancellationToken);
    }

    public async Task<EmergencyCallDetail> UpdateAsync(
        Guid id,
        UpdateEmergencyCallRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _lifecycle.LockCallAsync(id, cancellationToken);
        var call = await _db.EmergencyCalls.Include(item => item.Dispatches)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("Emergency call", id);
        if (call.Status.IsClosed()) throw new ConflictException(MessageCode.CallClosed);
        var sceneMoved = request.Latitude is { } latitude && request.Longitude is { } longitude
            && (latitude != call.Latitude || longitude != call.Longitude);
        var priorityChanged = request.Priority is { } newPriority && newPriority != call.Priority;

        if (request.Priority is { } priority)
        {
            call.Priority = priority;
        }

        if (request.Details is not null)
        {
            call.Details = Clean(request.Details);
        }

        if (request.CallerName is not null)
        {
            call.CallerName = Clean(request.CallerName);
        }

        if (request.CallerPhone is not null)
        {
            call.CallerPhone = Clean(request.CallerPhone);
        }

        if (sceneMoved)
        {
            call.Latitude = request.Latitude!.Value;
            call.Longitude = request.Longitude!.Value;
            call.LocationCapturedAt = _timeProvider.GetUtcNow();
            call.AddressLabel = null;
        }

        if (request.LocationAccuracyMetres is { } accuracy)
        {
            call.LocationAccuracyMetres = accuracy;
        }

        var needsNewRecommendation = (priorityChanged || sceneMoved)
            && call.Status == CallStatus.Received
            && !call.Dispatches.Any(dispatch => dispatch.Status.IsLive());

        if (needsNewRecommendation)
        {
            await _lifecycle.WithdrawOpenAsync(call.Id, DispatchWithdrawalReason.CallChanged, cancellationToken);
        }

        await SaveAsync(cancellationToken);
        DispatchProposal? recommendation = null;
        if (needsNewRecommendation)
        {
            var excluded = await _lifecycle.CarriedExclusionsAsync(call.Id, cancellationToken);
            recommendation = _lifecycle.Open(call.Id, call.Priority, allowDiversion: true, excluded);
            await SaveAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        if (recommendation is not null) _lifecycle.Wake(recommendation);
        if (sceneMoved) _sceneLookups.Enqueue(new AddressLookupJob(call.Id));

        return await DetailAsync(id, cancellationToken);
    }

    public async Task<EmergencyCallDetail> CancelAsync(
        Guid id,
        CancelEmergencyCallRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _lifecycle.LockCallAsync(id, cancellationToken);
        var call = await _db.EmergencyCalls.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("Emergency call", id);
        if (call.Status.IsClosed()) throw new ConflictException(MessageCode.CallNotCancellable);

        var notes = Clean(request.Notes);
        await _dispatches.StandDownForClosedCallAsync(id, notes, cancellationToken);
        var answeredRequest = call.CancellationRequestStatus == CancellationRequestStatus.Pending;
        if (answeredRequest)
        {
            ReviewCancellation(call, CancellationRequestStatus.Approved, notes);
        }

        call.Close(CallStatus.Cancelled, request.Outcome!.Value, notes, _timeProvider.GetUtcNow());
        await _lifecycle.WithdrawOpenAsync(call.Id, DispatchWithdrawalReason.CallClosed, cancellationToken);
        if (answeredRequest)
        {
            await NotifyCancellationAnsweredAsync(call, "approved", cancellationToken);
        }
        else
        {
            await _alerts.CallerAsync(call, NotificationType.EmergencyCallCancelled, "emergency_call", call.Id, cancellationToken);
        }

        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await DetailAsync(id, cancellationToken);
    }

    public async Task<MyCallTracking> TrackMineAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var callerId = PatientCallerId();
        var call = await _db.EmergencyCalls.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id, item.CallerUserId, item.Status, item.Latitude, item.Longitude, item.Outcome,
                item.CancellationRequestStatus, item.CancellationReviewNotes, item.UpdatedAt,
                HadEndedRun = item.Dispatches.Any(dispatch => !DispatchStatusExtensions.LiveStatuses.Contains(dispatch.Status)),
                Dispatch = item.Dispatches.Where(dispatch => DispatchStatusExtensions.LiveStatuses.Contains(dispatch.Status))
                    .Select(dispatch => new
                    {
                        dispatch.Id, dispatch.Status, dispatch.DispatchedAt, dispatch.Ambulance.RegistrationNumber,
                        dispatch.Ambulance.CurrentLatitude, dispatch.Ambulance.CurrentLongitude, dispatch.Ambulance.LocationUpdatedAt
                    })
                    .FirstOrDefault()
            }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Emergency call", id);
        if (call.CallerUserId != callerId) throw new ForbiddenException(MessageCode.Forbidden);

        var dispatch = call.Dispatch;
        var updatedAt = dispatch?.LocationUpdatedAt ?? dispatch?.DispatchedAt ?? call.UpdatedAt;
        var stale = dispatch is not null &&
            _timeProvider.GetUtcNow() - updatedAt > TimeSpan.FromMinutes(_options.LocationMaxAgeMinutes);
        var travel = dispatch is { CurrentLatitude: { } latitude, CurrentLongitude: { } longitude } && dispatch.Status.IsPrePickup()
            ? await ArrivalEstimateAsync(dispatch.Id, dispatch.LocationUpdatedAt, latitude, longitude,
                call.Latitude, call.Longitude, cancellationToken)
            : null;

        return new MyCallTracking
        {
            EmergencyCallId = call.Id,
            CallStatus = call.Status,
            DispatchStatus = dispatch?.Status,
            AmbulanceIsOnTheWay = dispatch?.Status.IsPrePickup() ?? false,
            LookingForAnotherAmbulance = call.Status == CallStatus.Received && dispatch is null && call.HadEndedRun,
            AmbulanceRegistration = dispatch?.RegistrationNumber,
            AmbulanceLatitude = dispatch?.CurrentLatitude,
            AmbulanceLongitude = dispatch?.CurrentLongitude,
            AmbulanceLocationIsStale = stale,
            EstimatedMinutesToArrival = travel?.DriveSeconds is { } seconds ? (int)Math.Ceiling(seconds / 60.0) : null,
            AmbulanceDistanceKm = travel is null ? null : Math.Round(travel.DistanceKm, 1),
            CancellationRequestStatus = call.CancellationRequestStatus,
            CancellationReviewNotes = call.CancellationReviewNotes,
            Outcome = call.Outcome,
            UpdatedAt = updatedAt
        };
    }

    public async Task<MyEmergencyCallSummary> CancelMineAsync(Guid id, RequestCancellationRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _lifecycle.LockCallAsync(id, cancellationToken);
        var call = await MineAsync(id, cancellationToken);
        if (call.Dispatches.Any(dispatch => dispatch.Status.IsLive())) throw new ConflictException(MessageCode.CallAlreadyDispatched);
        if (call.Status != CallStatus.Received) throw new ConflictException(MessageCode.CallNotCancellable);
        call.Close(CallStatus.Cancelled, EmergencyCallOutcome.CallerCancelled, request.Reason!.Trim(), _timeProvider.GetUtcNow());
        await _lifecycle.WithdrawOpenAsync(call.Id, DispatchWithdrawalReason.CallClosed, cancellationToken);
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToMine(call);
    }

    public async Task<EmergencyCancellationRequest> RequestCancellationAsync(Guid id, RequestCancellationRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _lifecycle.LockCallAsync(id, cancellationToken);
        var call = await MineAsync(id, cancellationToken);
        var live = call.Dispatches.FirstOrDefault(dispatch => dispatch.Status.IsLive())
            ?? throw new ConflictException(MessageCode.CallHasNoLiveDispatch);
        if (!live.Status.IsPrePickup())
            throw new ConflictException(MessageCode.RunPastPickup);
        if (call.CancellationRequestStatus == CancellationRequestStatus.Pending)
            throw new ConflictException(MessageCode.CancellationAlreadyRequested);
        call.CancellationRequestStatus = CancellationRequestStatus.Pending;
        call.CancellationRequestReason = request.Reason!.Trim();
        call.CancellationRequestedAt = _timeProvider.GetUtcNow();
        call.CancellationReviewedAt = null;
        call.CancellationReviewedByStaffId = null;
        call.CancellationReviewNotes = null;

        await _notifier.NotifyAsync(NotificationType.CancellationRequestWaiting, Recipients.Role(StaffRole.DutyManager),
            new NotificationSubject("emergency_call", call.Id), cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToCancellationRequest(call);
    }

    public async Task<PagedResult<EmergencyCancellationRequest>> ListCancellationRequestsAsync(
        CancellationRequestListRequest request, CancellationToken cancellationToken = default)
    {
        var query = _db.EmergencyCalls.AsNoTracking()
            .Where(call => call.CancellationRequestStatus != null);
        if (request.Status is { } status) query = query.Where(call => call.CancellationRequestStatus == status);
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(call => call.Dispatches)
                .ThenInclude(dispatch => dispatch.Ambulance)
            .OrderBy(call => call.CancellationRequestStatus == CancellationRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(call => call.CancellationRequestedAt)
            .ThenBy(call => call.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return PagedResult<EmergencyCancellationRequest>.From(
            items.Select(ToCancellationRequest).ToList(), request.Page, request.PageSize, totalItems);
    }

    public async Task<EmergencyCancellationRequest> ApproveCancellationRequestAsync(
        Guid id, ReviewCancellationRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _lifecycle.LockCallAsync(id, cancellationToken);
        var call = await LoadPendingCancellationAsync(id, cancellationToken);
        await _dispatches.StandDownForClosedCallAsync(id, call.CancellationRequestReason, cancellationToken);
        ReviewCancellation(call, CancellationRequestStatus.Approved, request.Notes);
        call.Close(CallStatus.Cancelled, EmergencyCallOutcome.CallerCancelled, call.CancellationRequestReason,
            _timeProvider.GetUtcNow());
        await _lifecycle.WithdrawOpenAsync(call.Id, DispatchWithdrawalReason.CallClosed, cancellationToken);
        await NotifyCancellationAnsweredAsync(call, "approved", cancellationToken);
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToCancellationRequest(call);
    }

    public async Task<EmergencyCancellationRequest> RejectCancellationRequestAsync(
        Guid id, ReviewCancellationRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _lifecycle.LockCallAsync(id, cancellationToken);
        var call = await LoadPendingCancellationAsync(id, cancellationToken);
        ReviewCancellation(call, CancellationRequestStatus.Rejected, request.Notes);
        await NotifyCancellationAnsweredAsync(call, "rejected", cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToCancellationRequest(call);
    }

    private Task NotifyCancellationAnsweredAsync(
        EmergencyCallEntity call, string outcome, CancellationToken cancellationToken)
        => _alerts.CallerAsync(call, NotificationType.CancellationAnswered, "emergency_call", call.Id, cancellationToken, outcome);

    private async Task<AmbulanceTravel?> ArrivalEstimateAsync(
        Guid dispatchId, DateTimeOffset? locationUpdatedAt, decimal fromLatitude, decimal fromLongitude,
        decimal sceneLatitude, decimal sceneLongitude, CancellationToken cancellationToken)
    {
        // The caller's screen polls every few seconds; ask the routing service only when the ambulance moved.
        var key = ("emergency-arrival-estimate", dispatchId, locationUpdatedAt);
        if (_cache.TryGetValue(key, out AmbulanceTravel? cached)) return cached;

        var measurement = await _distances.MeasureAsync(
            [new AmbulanceLocation(dispatchId, fromLatitude, fromLongitude)], sceneLatitude, sceneLongitude, cancellationToken);
        var travel = measurement.ByAmbulance.GetValueOrDefault(dispatchId);
        _cache.Set(key, travel, ArrivalEstimateLifetime);
        return travel;
    }

    private async Task<EmergencyCallDetail> DetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var call = await _db.EmergencyCalls.AsNoTracking().SingleAsync(item => item.Id == id, cancellationToken);
        var response = ToCall(call);
        response.LatestProposal = (await _proposals.LatestByCallAsync([call.Id], cancellationToken)).GetValueOrDefault(call.Id);
        var dispatches = await _dispatches.ListForCallAsync(call.Id, cancellationToken);
        response.Dispatches = _currentUser.PrincipalType == PrincipalType.Patient
            ? dispatches.Select(ForCaller).ToList()
            : dispatches;
        response.PatientName = call.PatientId is { } patientId
            ? (await _patients.FindByIdAsync(patientId, cancellationToken))?.FullName
            : null;
        return response;
    }

    // The caller sees each run's progress, never crew identity or clinical notes.
    private static DispatchDetail ForCaller(DispatchDetail dispatch) => new()
    {
        Id = dispatch.Id,
        EmergencyCallId = dispatch.EmergencyCallId,
        AmbulanceRegistration = dispatch.AmbulanceRegistration,
        CallPriority = dispatch.CallPriority,
        Status = dispatch.Status,
        CrewCount = dispatch.CrewCount,
        AcknowledgementOverdue = dispatch.AcknowledgementOverdue,
        DispatchedAt = dispatch.DispatchedAt,
        CompletedAt = dispatch.CompletedAt
    };

    private async Task<EmergencyCallEntity> MineAsync(Guid id, CancellationToken cancellationToken)
    {
        var call = await _db.EmergencyCalls.Include(call => call.Dispatches)
            .ThenInclude(dispatch => dispatch.Ambulance)
            .SingleOrDefaultAsync(call => call.Id == id, cancellationToken)
            ?? throw new NotFoundException("Emergency call", id);
        if (call.CallerUserId != PatientCallerId()) throw new ForbiddenException(MessageCode.Forbidden);
        return call;
    }

    private async Task<EmergencyCallEntity> LoadPendingCancellationAsync(Guid id, CancellationToken cancellationToken)
    {
        var call = await _db.EmergencyCalls
            .Include(item => item.Dispatches)
                .ThenInclude(dispatch => dispatch.Ambulance)
            .SingleOrDefaultAsync(call => call.Id == id, cancellationToken)
            ?? throw new NotFoundException("Emergency call", id);
        if (call.CancellationRequestStatus != CancellationRequestStatus.Pending)
            throw new ConflictException(MessageCode.CancellationNotPending);
        return call;
    }

    private void ReviewCancellation(EmergencyCallEntity call, CancellationRequestStatus status, string? notes)
    {
        call.CancellationRequestStatus = status;
        call.CancellationReviewedAt = _timeProvider.GetUtcNow();
        call.CancellationReviewedByStaffId = _currentUser.Id;
        call.CancellationReviewNotes = Clean(notes);
    }

    private static MyEmergencyCallSummary ToMine(EmergencyCallEntity call) => new()
    {
        Id = call.Id, PatientIsCaller = call.PatientIsCaller, Priority = call.Priority,
        Status = call.Status, CancellationRequestStatus = call.CancellationRequestStatus, CreatedAt = call.CreatedAt
    };

    private static EmergencyCancellationRequest ToCancellationRequest(EmergencyCallEntity call)
    {
        var live = call.Dispatches
            .Where(dispatch => dispatch.Status.IsLive())
            .OrderByDescending(dispatch => dispatch.DispatchedAt)
            .FirstOrDefault();
        return new EmergencyCancellationRequest
        {
            EmergencyCallId = call.Id,
            CallPriority = call.Priority,
            CallStatus = call.Status,
            CallerName = call.CallerName,
            AddressLabel = call.AddressLabel,
            CallCreatedAt = call.CreatedAt,
            ActiveAmbulanceRegistration = live?.Ambulance.RegistrationNumber,
            ActiveDispatchStatus = live?.Status,
            CanApprove = call.CancellationRequestStatus == CancellationRequestStatus.Pending
                && !call.Status.IsClosed()
                && (live is null || live.Status.IsPrePickup()),
            Status = call.CancellationRequestStatus!.Value,
            Reason = call.CancellationRequestReason!,
            RequestedAt = call.CancellationRequestedAt!.Value,
            ReviewedAt = call.CancellationReviewedAt,
            ReviewedByStaffId = call.CancellationReviewedByStaffId,
            ReviewNotes = call.CancellationReviewNotes
        };
    }

    private static IQueryable<EmergencyCallEntity> Order(
        IQueryable<EmergencyCallEntity> query,
        EmergencyCallSortField sortBy,
        bool ascending)
        => sortBy switch
        {
            EmergencyCallSortField.CreatedAt => ascending
                ? query.OrderBy(call => call.CreatedAt).ThenBy(call => call.Id)
                : query.OrderByDescending(call => call.CreatedAt).ThenBy(call => call.Id),
            EmergencyCallSortField.Status => ascending
                ? query.OrderBy(call => call.Status).ThenBy(call => call.Id)
                : query.OrderByDescending(call => call.Status).ThenBy(call => call.Id),
            _ => ascending
                ? query.OrderByDescending(call => call.Priority).ThenBy(call => call.CreatedAt).ThenBy(call => call.Id)
                : query.OrderBy(call => call.Priority).ThenBy(call => call.CreatedAt).ThenBy(call => call.Id)
        };

    private Guid PatientCallerId()
    {
        if (_currentUser.PrincipalType != PrincipalType.Patient)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }

        return _currentUser.Id;
    }

    private static void EnsureSameCaller(
        EmergencyCallEntity existing,
        PrincipalType principalType,
        Guid principalId)
    {
        var sameCaller = principalType == PrincipalType.Patient
            ? existing.CallerUserId == principalId
            : existing.CallerUserId is null;
        if (!sameCaller)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }
    }

    private static EmergencyCallDetail ToCall(EmergencyCallEntity call) => new()
    {
        Id = call.Id,
        PatientId = call.PatientId,
        CallerUserId = call.CallerUserId,
        PatientIsCaller = call.PatientIsCaller,
        CallerName = call.CallerName,
        CallerPhone = call.CallerPhone,
        Latitude = call.Latitude,
        Longitude = call.Longitude,
        LocationAccuracyMetres = call.LocationAccuracyMetres,
        LocationCapturedAt = call.LocationCapturedAt,
        IdempotencyKey = call.IdempotencyKey,
        AddressLabel = call.AddressLabel,
        Details = call.Details,
        Priority = call.Priority,
        Status = call.Status,
        Outcome = call.Outcome,
        OutcomeNotes = call.OutcomeNotes,
        ClosedAt = call.ClosedAt,
        Transported = call.Transported,
        CancellationRequestStatus = call.CancellationRequestStatus,
        CreatedAt = call.CreatedAt,
        UpdatedAt = call.UpdatedAt
    };

    private static EmergencyCallSummary ToSummary(
        EmergencyCallEntity call,
        Guid? activeDispatchId,
        DispatchProposalSummary? latestProposal,
        DateTimeOffset now) => new()
    {
        Id = call.Id,
        Priority = call.Priority,
        Status = call.Status,
        CallerName = call.CallerName,
        AddressLabel = call.AddressLabel,
        Latitude = call.Latitude,
        Longitude = call.Longitude,
        ActiveDispatchId = activeDispatchId,
        LatestProposal = latestProposal,
        WaitingMinutes = call.WaitingMinutes(now),
        CreatedAt = call.CreatedAt
    };

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(MessageCode.DispatchProposalConflict);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: DispatchProposalConfiguration.OpenPerCallUniqueIndex
        })
        {
            throw new ConflictException(MessageCode.DispatchProposalConflict);
        }
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
