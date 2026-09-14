using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using EmergencyCallEntity = CareLanka.Api.Data.Entities.Emergency.EmergencyCall;

namespace CareLanka.Api.Services.Emergency;

public sealed class EmergencyCallService : IEmergencyCallService
{
    private readonly CareLankaDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public EmergencyCallService(
        CareLankaDbContext db,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
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

        await EnsurePatientLinkBelongsToCallerAsync(request, principalType, principalId, cancellationToken);

        var call = new EmergencyCallEntity
        {
            Id = Guid.NewGuid(),
            PatientId = request.PatientId,
            CallerUserId = callerUserId,
            PatientIsCaller = request.PatientIsCaller!.Value,
            CallerName = Clean(request.CallerName),
            CallerPhone = Clean(request.CallerPhone),
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
            _db.Entry(call).State = EntityState.Detached;
            existing = await _db.EmergencyCalls
                .AsNoTracking()
                .SingleAsync(stored => stored.IdempotencyKey == key, cancellationToken);
            EnsureSameCaller(existing, principalType, principalId);
            return await DetailAsync(existing.Id, cancellationToken);
        }

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
            var term = request.Search.Trim();
            query = query.Where(call =>
                (call.CallerName != null && EF.Functions.ILike(call.CallerName, $"%{term}%"))
                || (call.CallerPhone != null && EF.Functions.ILike(call.CallerPhone, $"%{term}%"))
                || (call.Details != null && EF.Functions.ILike(call.Details, $"%{term}%")));
        }

        if (request.From is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(call => call.CreatedAt >= start);
        }

        if (request.To is { } to)
        {
            var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(call => call.CreatedAt < end);
        }

        if (request.UnassignedOnly)
        {
            query = query.Where(call => !call.Dispatches.Any(dispatch =>
                dispatch.Status == DispatchStatus.Assigned || dispatch.Status == DispatchStatus.EnRoute));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        query = Order(query, request.SortBy, request.SortDir == "asc");
        var rows = await query.Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(call => new
            {
                Call = call,
                ActiveDispatchId = call.Dispatches
                    .Where(dispatch => dispatch.Status == DispatchStatus.Assigned
                        || dispatch.Status == DispatchStatus.EnRoute)
                    .Select(dispatch => (Guid?)dispatch.Id)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var items = rows.Select(row => ToSummary(row.Call, row.ActiveDispatchId, now)).ToList();
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

        if (_currentUser.PrincipalType == PrincipalType.Staff
            && _currentUser.Role is not (PrincipalRole.DutyManager or PrincipalRole.AmbulanceCrew))
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }

        return await DetailAsync(id, cancellationToken);
    }

    public async Task<EmergencyCallDetail> UpdateAsync(
        Guid id,
        UpdateEmergencyCallRequest request,
        CancellationToken cancellationToken = default)
    {
        var call = await _db.EmergencyCalls.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("Emergency call", id);

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

        if (request.Latitude is { } latitude && request.Longitude is { } longitude)
        {
            call.Latitude = latitude;
            call.Longitude = longitude;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await DetailAsync(id, cancellationToken);
    }

    private async Task<EmergencyCallDetail> DetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var call = await _db.EmergencyCalls.AsNoTracking()
            .Include(item => item.Dispatches).ThenInclude(dispatch => dispatch.Ambulance)
            .Include(item => item.Dispatches).ThenInclude(dispatch => dispatch.Crew)
            .SingleAsync(item => item.Id == id, cancellationToken);
        var response = ToCall(call);
        response.Dispatches = call.Dispatches.OrderBy(dispatch => dispatch.DispatchedAt)
            .Select(dispatch => new DispatchSummary
            {
                Id = dispatch.Id,
                EmergencyCallId = call.Id,
                AmbulanceRegistration = dispatch.Ambulance.RegistrationNumber,
                CallPriority = call.Priority,
                Status = dispatch.Status,
                CrewCount = dispatch.Crew.Count,
                DispatchedAt = dispatch.DispatchedAt,
                CompletedAt = dispatch.CompletedAt
            })
            .ToList();
        return response;
    }

    private async Task EnsurePatientLinkBelongsToCallerAsync(
        CreateEmergencyCallRequest request,
        PrincipalType principalType,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        if (principalType != PrincipalType.Patient
            || !request.PatientIsCaller!.Value
            || request.PatientId is null)
        {
            return;
        }

        var belongsToCaller = await _db.Patients.AsNoTracking().AnyAsync(
            patient => patient.Id == request.PatientId && patient.UserAccountId == principalId,
            cancellationToken);
        if (!belongsToCaller)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }
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
        Transported = call.Transported,
        CreatedAt = call.CreatedAt,
        UpdatedAt = call.UpdatedAt
    };

    private static EmergencyCallSummary ToSummary(
        EmergencyCallEntity call,
        Guid? activeDispatchId,
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
        WaitingMinutes = activeDispatchId is null
            ? Math.Max(0, (int)(now - call.CreatedAt).TotalMinutes)
            : 0,
        CreatedAt = call.CreatedAt
    };

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
