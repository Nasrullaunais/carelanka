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
    private readonly IAmbulanceEligibilityService _eligibility;
    private readonly IAmbulanceCrewService _crew;
    private readonly TimeProvider _timeProvider;

    public AmbulanceService(
        CareLankaDbContext db,
        ICurrentUser currentUser,
        IAmbulanceDistanceService distances,
        IAmbulanceEligibilityService eligibility,
        IAmbulanceCrewService crew,
        TimeProvider timeProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _distances = distances;
        _eligibility = eligibility;
        _crew = crew;
        _timeProvider = timeProvider;
    }

    public async Task<PagedResult<AmbulanceSummary>> ListAsync(
        AmbulanceListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Ambulances.AsNoTracking();

        if (!request.IncludeRetired)
        {
            query = query.Where(ambulance => ambulance.IsActive);
        }

        if (request.Status is { } wantedStatus)
        {
            query = query.Where(ambulance => ambulance.Status == wantedStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(ambulance => EF.Functions.ILike(ambulance.RegistrationNumber, $"%{term}%"));
        }

        var rows = await query.ToListAsync(cancellationToken);
        var measurement = request.NearToLatitude is not null && request.NearToLongitude is not null
            ? await _distances.MeasureAsync(
                rows.Select(ambulance => new AmbulanceLocation(
                    ambulance.Id,
                    ambulance.CurrentLatitude,
                    ambulance.CurrentLongitude)).ToList(),
                request.NearToLatitude.Value,
                request.NearToLongitude.Value,
                cancellationToken)
            : null;
        var activeDispatches = await _db.Dispatches
            .AsNoTracking()
            .Where(dispatch => DispatchStatusExtensions.LiveStatuses.Contains(dispatch.Status))
            .Select(dispatch => new { dispatch.AmbulanceId, dispatch.Id })
            .ToDictionaryAsync(dispatch => dispatch.AmbulanceId, dispatch => dispatch.Id, cancellationToken);
        var crewCounts = await _db.AmbulanceCrewAssignments
            .AsNoTracking()
            .Where(assignment => assignment.UnassignedAt == null)
            .GroupBy(assignment => assignment.AmbulanceId)
            .Select(group => new { AmbulanceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.AmbulanceId, item => item.Count, cancellationToken);

        var summaries = rows.Select(ambulance =>
        {
            Guid? activeDispatchId = activeDispatches.TryGetValue(ambulance.Id, out var dispatchId)
                ? dispatchId
                : null;
            var crewCount = crewCounts.GetValueOrDefault(ambulance.Id);
            AmbulanceTravel? travel = null;
            measurement?.ByAmbulance.TryGetValue(ambulance.Id, out travel);
            var decision = _eligibility.Decide(new AmbulanceEligibilityFacts(
                ambulance.IsActive,
                ambulance.Status,
                crewCount,
                activeDispatchId,
                ambulance.CurrentLatitude.HasValue && ambulance.CurrentLongitude.HasValue,
                ambulance.LocationUpdatedAt));
            return new AmbulanceSummary
            {
                Id = ambulance.Id,
                RegistrationNumber = ambulance.RegistrationNumber,
                Status = ambulance.Status,
                CurrentLatitude = ambulance.CurrentLatitude,
                CurrentLongitude = ambulance.CurrentLongitude,
                LocationUpdatedAt = ambulance.LocationUpdatedAt,
                CurrentCrewCount = crewCount,
                RequiredCrewCount = decision.RequiredCrewCount,
                IsEligible = decision.IsEligible,
                EligibilityBlockReasons = decision.BlockReasons,
                ActiveDispatchId = activeDispatchId,
                IsDivertible = activeDispatchId is null || IsDivertible(ambulance.Status),
                DistanceKm = travel?.DistanceKm,
                DriveMinutes = travel?.DriveSeconds is { } seconds ? (int)Math.Ceiling(seconds / 60.0) : null,
                IsStraightLineDistance = measurement?.IsStraightLine
            };
        });

        if (request.EligibleOnly)
        {
            summaries = summaries.Where(ambulance => ambulance.IsEligible);
        }

        var totalItems = summaries.Count();

        summaries = (request.SortBy, request.SortDir.Equals("asc", StringComparison.OrdinalIgnoreCase)) switch
        {
            (AmbulanceSortField.Status, true) => summaries.OrderBy(ambulance => ambulance.Status).ThenBy(ambulance => ambulance.Id),
            (AmbulanceSortField.Status, false) => summaries.OrderByDescending(ambulance => ambulance.Status).ThenBy(ambulance => ambulance.Id),
            (AmbulanceSortField.Distance, true) => summaries.OrderBy(ambulance => ambulance.DistanceKm is null).ThenBy(ambulance => ambulance.DriveMinutes).ThenBy(ambulance => ambulance.DistanceKm).ThenBy(ambulance => ambulance.Id),
            (AmbulanceSortField.Distance, false) => summaries.OrderBy(ambulance => ambulance.DistanceKm is null).ThenByDescending(ambulance => ambulance.DriveMinutes).ThenByDescending(ambulance => ambulance.DistanceKm).ThenBy(ambulance => ambulance.Id),
            (AmbulanceSortField.RegistrationNumber, true) => summaries.OrderBy(ambulance => ambulance.RegistrationNumber).ThenBy(ambulance => ambulance.Id),
            _ => summaries.OrderByDescending(ambulance => ambulance.RegistrationNumber).ThenBy(ambulance => ambulance.Id)
        };

        var items = summaries.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        return PagedResult<AmbulanceSummary>.From(items, request.Page, request.PageSize, totalItems);
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
            LocationUpdatedAt = ambulance.LocationUpdatedAt,
            Status = ambulance.Status,
            OutOfServiceReason = ambulance.OutOfServiceReason,
            IsActive = ambulance.IsActive,
            CreatedAt = ambulance.CreatedAt,
            UpdatedAt = ambulance.UpdatedAt,
            IsDivertible = IsDivertible(ambulance.Status),
            RunsToday = runsToday,
            CurrentCrew = await _crew.ListCurrentForFleetAsync(id, cancellationToken)
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
            LocationUpdatedAt = request.CurrentLatitude.HasValue ? _timeProvider.GetUtcNow() : null,
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

    public async Task ReportLocationAsync(Guid id, ReportAmbulanceLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var ambulance = await GetEntityAsync(id, cancellationToken);
        var ownsLiveRun = await _db.DispatchCrew.AnyAsync(crew =>
            crew.StaffMemberId == _currentUser.Id && crew.Dispatch.AmbulanceId == id
            && DispatchStatusExtensions.LiveStatuses.Contains(crew.Dispatch.Status), cancellationToken);
        if (!ownsLiveRun) throw new ForbiddenException(MessageCode.Forbidden);
        ambulance.CurrentLatitude = request.Latitude!.Value;
        ambulance.CurrentLongitude = request.Longitude!.Value;
        ambulance.LocationUpdatedAt = _timeProvider.GetUtcNow();
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static AmbulanceResponse ToResponse(AmbulanceEntity ambulance) => new()
    {
        Id = ambulance.Id,
        RegistrationNumber = ambulance.RegistrationNumber,
        CurrentLatitude = ambulance.CurrentLatitude,
        CurrentLongitude = ambulance.CurrentLongitude,
        LocationUpdatedAt = ambulance.LocationUpdatedAt,
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
            && DispatchStatusExtensions.LiveStatuses.Contains(crew.Dispatch.Status), cancellationToken);

        if (!assigned)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }
    }

    private async Task EnsureNoActiveDispatchAsync(Guid ambulanceId, CancellationToken cancellationToken)
    {
        var active = await _db.Dispatches.AnyAsync(dispatch =>
            dispatch.AmbulanceId == ambulanceId
            && DispatchStatusExtensions.LiveStatuses.Contains(dispatch.Status), cancellationToken);

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
