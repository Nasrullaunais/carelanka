using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data;
using CareLanka.Api.Data.Configurations.Emergency;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using CrewAssignmentEntity = CareLanka.Api.Data.Entities.Emergency.AmbulanceCrewAssignment;
using CrewAssignmentResponse = CareLanka.Api.DTOs.Emergency.AmbulanceCrewAssignment;

namespace CareLanka.Api.Services.Emergency;

public sealed class AmbulanceCrewService : IAmbulanceCrewService
{
    private readonly CareLankaDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IStaffLookupService _staffLookup;
    private readonly TimeProvider _timeProvider;

    public AmbulanceCrewService(
        CareLankaDbContext db,
        ICurrentUser currentUser,
        IStaffLookupService staffLookup,
        TimeProvider timeProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _staffLookup = staffLookup;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<CrewAssignmentResponse>> ListCurrentAsync(
        Guid ambulanceId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAmbulanceExistsAsync(ambulanceId, cancellationToken);
        var assignments = await _db.AmbulanceCrewAssignments
            .AsNoTracking()
            .Where(assignment => assignment.AmbulanceId == ambulanceId
                && assignment.UnassignedAt == null)
            .OrderBy(assignment => assignment.AssignedAt)
            .ToListAsync(cancellationToken);

        if (_currentUser.Role != PrincipalRole.DutyManager
            && assignments.All(assignment => assignment.StaffMemberId != _currentUser.Id))
        {
            throw new ForbiddenException();
        }

        return await ToResponsesAsync(assignments, cancellationToken);
    }

    public async Task<IReadOnlyList<CrewAssignmentResponse>> ListCurrentForFleetAsync(
        Guid ambulanceId,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _db.AmbulanceCrewAssignments
            .AsNoTracking()
            .Where(assignment => assignment.AmbulanceId == ambulanceId
                && assignment.UnassignedAt == null)
            .OrderBy(assignment => assignment.AssignedAt)
            .ToListAsync(cancellationToken);
        return await ToResponsesAsync(assignments, cancellationToken);
    }

    public async Task<CrewAssignmentResponse> AssignAsync(
        Guid ambulanceId,
        AssignAmbulanceCrewRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureAmbulanceExistsAsync(ambulanceId, cancellationToken);
        await EnsureNoLiveDispatchAsync(ambulanceId, cancellationToken);

        var staff = (await _staffLookup.LookupAsync([request.StaffMemberId], cancellationToken))[0];
        if (!staff.Found)
        {
            throw new NotFoundException("Staff member", request.StaffMemberId);
        }

        if (staff.Role != StaffRole.AmbulanceCrew || staff.IsActive != true)
        {
            throw new BadRequestException(MessageCode.StaffNotAmbulanceCrew, request.StaffMemberId);
        }

        var assignment = new CrewAssignmentEntity
        {
            Id = Guid.NewGuid(),
            AmbulanceId = ambulanceId,
            StaffMemberId = request.StaffMemberId,
            AssignedAt = _timeProvider.GetUtcNow(),
            AssignedByStaffId = _currentUser.Id
        };
        _db.AmbulanceCrewAssignments.Add(assignment);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: AmbulanceCrewAssignmentConfiguration.CurrentStaffUniqueIndex
                or AmbulanceCrewAssignmentConfiguration.CurrentAmbulanceStaffUniqueIndex
        })
        {
            throw new ConflictException(MessageCode.CrewMemberAlreadyAssigned, request.StaffMemberId);
        }

        return new CrewAssignmentResponse
        {
            Id = assignment.Id,
            AmbulanceId = assignment.AmbulanceId,
            StaffMemberId = assignment.StaffMemberId,
            FullName = staff.FullName,
            AssignedAt = assignment.AssignedAt,
            AssignedByStaffId = assignment.AssignedByStaffId
        };
    }

    public async Task UnassignAsync(
        Guid ambulanceId,
        Guid staffMemberId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAmbulanceExistsAsync(ambulanceId, cancellationToken);
        await EnsureNoLiveDispatchAsync(ambulanceId, cancellationToken);
        var assignment = await _db.AmbulanceCrewAssignments.SingleOrDefaultAsync(
            current => current.AmbulanceId == ambulanceId
                && current.StaffMemberId == staffMemberId
                && current.UnassignedAt == null,
            cancellationToken) ?? throw new NotFoundException("Current crew assignment", staffMemberId);

        assignment.UnassignedAt = _timeProvider.GetUtcNow();
        assignment.UnassignedByStaffId = _currentUser.Id;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<CrewAssignmentResponse>> ToResponsesAsync(
        IReadOnlyList<CrewAssignmentEntity> assignments,
        CancellationToken cancellationToken)
    {
        var people = (await _staffLookup.LookupAsync(
            assignments.Select(assignment => assignment.StaffMemberId).ToList(),
            cancellationToken)).ToDictionary(person => person.StaffId);

        return assignments.Select(assignment => new CrewAssignmentResponse
        {
            Id = assignment.Id,
            AmbulanceId = assignment.AmbulanceId,
            StaffMemberId = assignment.StaffMemberId,
            FullName = people.GetValueOrDefault(assignment.StaffMemberId)?.FullName,
            AssignedAt = assignment.AssignedAt,
            AssignedByStaffId = assignment.AssignedByStaffId,
            UnassignedAt = assignment.UnassignedAt,
            UnassignedByStaffId = assignment.UnassignedByStaffId
        }).ToList();
    }

    private async Task EnsureAmbulanceExistsAsync(Guid ambulanceId, CancellationToken cancellationToken)
    {
        if (!await _db.Ambulances.AnyAsync(ambulance => ambulance.Id == ambulanceId, cancellationToken))
        {
            throw new NotFoundException("Ambulance", ambulanceId);
        }
    }

    private async Task EnsureNoLiveDispatchAsync(Guid ambulanceId, CancellationToken cancellationToken)
    {
        var hasLiveDispatch = await _db.Dispatches.AnyAsync(dispatch =>
            dispatch.AmbulanceId == ambulanceId
            && (dispatch.Status == DispatchStatus.Assigned || dispatch.Status == DispatchStatus.EnRoute),
            cancellationToken);
        if (hasLiveDispatch)
        {
            throw new ConflictException(MessageCode.AmbulanceHasActiveDispatch);
        }
    }
}
