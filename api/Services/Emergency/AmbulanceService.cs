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
using AmbulanceEntity = CareLanka.Api.Data.Entities.Emergency.Ambulance;
using AmbulanceResponse = CareLanka.Api.DTOs.Emergency.Ambulance;

namespace CareLanka.Api.Services.Emergency;

public sealed class AmbulanceService : IAmbulanceService
{
    private readonly CareLankaDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAmbulanceDistanceService _distances;

    public AmbulanceService(
        CareLankaDbContext db,
        ICurrentUser currentUser,
        IAmbulanceDistanceService distances)
    {
        _db = db;
        _currentUser = currentUser;
        _distances = distances;
    }

    public async Task<PagedResult<AmbulanceSummary>> ListAsync(
        AmbulanceStatus? status,
        string? search,
        decimal? nearToLatitude,
        decimal? nearToLongitude,
        bool includeRetired,
        int page,
        int pageSize,
        AmbulanceSortField sortBy,
        string sortDir,
        CancellationToken cancellationToken = default)
    {
        if (nearToLatitude.HasValue != nearToLongitude.HasValue
            || sortBy == AmbulanceSortField.Distance && nearToLatitude is null)
        {
            throw new BadRequestException(MessageCode.ValidationFailed);
        }

        var query = _db.Ambulances.AsNoTracking();

        if (!includeRetired)
        {
            query = query.Where(ambulance => ambulance.IsActive);
        }

        if (status is { } wantedStatus)
        {
            query = query.Where(ambulance => ambulance.Status == wantedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(ambulance => EF.Functions.ILike(ambulance.RegistrationNumber, $"%{term}%"));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var rows = await query.ToListAsync(cancellationToken);
        var measuredDistances = nearToLatitude is not null && nearToLongitude is not null
            ? await _distances.MeasureAsync(
                rows.Select(ambulance => new AmbulanceLocation(
                    ambulance.Id,
                    ambulance.CurrentLatitude,
                    ambulance.CurrentLongitude)).ToList(),
                nearToLatitude.Value,
                nearToLongitude.Value,
                cancellationToken)
            : new Dictionary<Guid, double?>();
        var activeDispatches = await _db.Dispatches
            .AsNoTracking()
            .Where(dispatch => dispatch.Status == DispatchStatus.Assigned
                || dispatch.Status == DispatchStatus.EnRoute)
            .Select(dispatch => new { dispatch.AmbulanceId, dispatch.Id })
            .ToDictionaryAsync(dispatch => dispatch.AmbulanceId, dispatch => dispatch.Id, cancellationToken);

        var summaries = rows.Select(ambulance => new AmbulanceSummary
        {
            Id = ambulance.Id,
            RegistrationNumber = ambulance.RegistrationNumber,
            Status = ambulance.Status,
            CurrentLatitude = ambulance.CurrentLatitude,
            CurrentLongitude = ambulance.CurrentLongitude,
            ActiveDispatchId = activeDispatches.GetValueOrDefault(ambulance.Id),
            IsDivertible = IsDivertible(ambulance.Status),
            DistanceKm = measuredDistances.GetValueOrDefault(ambulance.Id)
        });

        summaries = (sortBy, sortDir.Equals("asc", StringComparison.OrdinalIgnoreCase)) switch
        {
            (AmbulanceSortField.Status, true) => summaries.OrderBy(ambulance => ambulance.Status).ThenBy(ambulance => ambulance.Id),
            (AmbulanceSortField.Status, false) => summaries.OrderByDescending(ambulance => ambulance.Status).ThenBy(ambulance => ambulance.Id),
            (AmbulanceSortField.Distance, true) => summaries.OrderBy(ambulance => ambulance.DistanceKm).ThenBy(ambulance => ambulance.Id),
            (AmbulanceSortField.Distance, false) => summaries.OrderByDescending(ambulance => ambulance.DistanceKm).ThenBy(ambulance => ambulance.Id),
            (AmbulanceSortField.RegistrationNumber, true) => summaries.OrderBy(ambulance => ambulance.RegistrationNumber).ThenBy(ambulance => ambulance.Id),
            _ => summaries.OrderByDescending(ambulance => ambulance.RegistrationNumber).ThenBy(ambulance => ambulance.Id)
        };

        var items = summaries.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return PagedResult<AmbulanceSummary>.From(items, page, pageSize, totalItems);
    }

    public async Task<AmbulanceDetail> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var ambulance = await FindByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Ambulance", id);
        var startOfDay = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var runsToday = await _db.Dispatches.CountAsync(
            dispatch => dispatch.AmbulanceId == id && dispatch.DispatchedAt >= startOfDay,
            cancellationToken);

        return new AmbulanceDetail
        {
            Id = ambulance.Id,
            RegistrationNumber = ambulance.RegistrationNumber,
            CurrentLatitude = ambulance.CurrentLatitude,
            CurrentLongitude = ambulance.CurrentLongitude,
            Status = ambulance.Status,
            OutOfServiceReason = ambulance.OutOfServiceReason,
            IsActive = ambulance.IsActive,
            CreatedAt = ambulance.CreatedAt,
            UpdatedAt = ambulance.UpdatedAt,
            IsDivertible = IsDivertible(ambulance.Status),
            RunsToday = runsToday
        };
    }

    public async Task<AmbulanceResponse> CreateAsync(
        CreateAmbulanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var registrationNumber = request.RegistrationNumber.Trim().ToUpperInvariant();

        if (registrationNumber.Length == 0)
        {
            throw new BadRequestException(MessageCode.ValidationFailed);
        }

        var ambulance = new AmbulanceEntity
        {
            Id = Guid.NewGuid(),
            RegistrationNumber = registrationNumber,
            CurrentLatitude = request.CurrentLatitude,
            CurrentLongitude = request.CurrentLongitude,
            Status = AmbulanceStatus.Available
        };

        _db.Ambulances.Add(ambulance);

        await SaveAsync(registrationNumber, cancellationToken);

        return ToResponse(ambulance);
    }

    public async Task<AmbulanceResponse> UpdateAsync(
        Guid id,
        UpdateAmbulanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var ambulance = await GetEntityAsync(id, cancellationToken);
        await EnsureMayUpdateAsync(ambulance.Id, cancellationToken);

        if (request.Status == AmbulanceStatus.OutOfService)
        {
            await EnsureNoActiveDispatchAsync(ambulance.Id, cancellationToken);
        }

        if (request.RegistrationNumber is not null)
        {
            var registrationNumber = request.RegistrationNumber.Trim().ToUpperInvariant();
            if (registrationNumber.Length == 0)
            {
                throw new BadRequestException(MessageCode.ValidationFailed);
            }

            ambulance.RegistrationNumber = registrationNumber;
        }

        if (request.Status is { } status)
        {
            ambulance.Status = status;
            if (status != AmbulanceStatus.OutOfService)
            {
                ambulance.OutOfServiceReason = null;
            }
        }

        if (request.OutOfServiceReason is not null)
        {
            ambulance.OutOfServiceReason = request.OutOfServiceReason.Trim();
        }

        await SaveAsync(ambulance.RegistrationNumber, cancellationToken);
        return ToResponse(ambulance);
    }

    public async Task RetireAsync(
        Guid id,
        RetireAmbulanceRequest request,
        CancellationToken cancellationToken = default)
    {
        var ambulance = await GetEntityAsync(id, cancellationToken);
        await EnsureNoActiveDispatchAsync(id, cancellationToken);
        var reason = request.Reason.Trim();

        if (reason.Length == 0)
        {
            throw new BadRequestException(MessageCode.ValidationFailed);
        }

        ambulance.Status = AmbulanceStatus.OutOfService;
        ambulance.OutOfServiceReason = reason;
        ambulance.IsActive = false;
        ambulance.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AmbulanceResponse> ReinstateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var ambulance = await GetEntityAsync(id, cancellationToken);
        ambulance.Status = AmbulanceStatus.Available;
        ambulance.OutOfServiceReason = null;
        ambulance.IsActive = true;
        ambulance.DeletedAt = null;
        await SaveAsync(ambulance.RegistrationNumber, cancellationToken);
        return ToResponse(ambulance);
    }

    private static AmbulanceResponse ToResponse(AmbulanceEntity ambulance) => new()
    {
        Id = ambulance.Id,
        RegistrationNumber = ambulance.RegistrationNumber,
        CurrentLatitude = ambulance.CurrentLatitude,
        CurrentLongitude = ambulance.CurrentLongitude,
        Status = ambulance.Status,
        OutOfServiceReason = ambulance.OutOfServiceReason,
        IsActive = ambulance.IsActive,
        CreatedAt = ambulance.CreatedAt,
        UpdatedAt = ambulance.UpdatedAt
    };

    private Task<AmbulanceEntity?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
        => _db.Ambulances.FirstOrDefaultAsync(ambulance => ambulance.Id == id, cancellationToken);

    private async Task<AmbulanceEntity> GetEntityAsync(Guid id, CancellationToken cancellationToken)
        => await FindByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Ambulance", id);

    private async Task EnsureMayUpdateAsync(Guid ambulanceId, CancellationToken cancellationToken)
    {
        if (_currentUser.Role == PrincipalRole.DutyManager)
        {
            return;
        }

        var assigned = await _db.DispatchCrew.AnyAsync(crew =>
            crew.StaffMemberId == _currentUser.Id
            && crew.Dispatch.AmbulanceId == ambulanceId
            && (crew.Dispatch.Status == DispatchStatus.Assigned
                || crew.Dispatch.Status == DispatchStatus.EnRoute), cancellationToken);

        if (!assigned)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }
    }

    private async Task EnsureNoActiveDispatchAsync(Guid ambulanceId, CancellationToken cancellationToken)
    {
        var active = await _db.Dispatches.AnyAsync(dispatch =>
            dispatch.AmbulanceId == ambulanceId
            && (dispatch.Status == DispatchStatus.Assigned
                || dispatch.Status == DispatchStatus.EnRoute), cancellationToken);

        if (active)
        {
            throw new ConflictException(MessageCode.AmbulanceHasActiveDispatch);
        }
    }

    private async Task SaveAsync(string registrationNumber, CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: AmbulanceConfiguration.RegistrationNumberUniqueIndex
        })
        {
            throw new ConflictException(MessageCode.AmbulanceRegistrationTaken, registrationNumber);
        }
    }

    private static bool IsDivertible(AmbulanceStatus status)
        => status is AmbulanceStatus.Available or AmbulanceStatus.Dispatched or AmbulanceStatus.EnRoute;

}
