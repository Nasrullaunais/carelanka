using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Agents.Staff;

public sealed record StaffCandidate(
    Guid Id,
    string FullName,
    StaffRole Role,
    string? Department,
    bool IsActive);

public sealed record UnderstaffedShiftInfo(
    Shift Shift,
    string WardName,
    int ConfirmedCount,
    int MinimumHeadcount,
    int HeadcountNeeded,
    int GapToMinimum);

public sealed record CascadingSwapCandidate(
    StaffCandidate Staff,
    Allocation SourceAllocation,
    Shift SourceShift,
    string SourceWardName,
    int DonorConfirmedCount,
    int DonorMinimumHeadcount,
    int SurplusCount);

/// <summary>
/// Allow-listed, read-only tools for the Staff Allocation Agent.
/// Zero database write privileges: every proposal is evaluated in memory and submitted
/// for human authorization before any allocations are applied.
/// </summary>
public interface IStaffAllocationAgentTools
{
    /// <summary>
    /// Reads the understaffed target shift with its required role, required skill, and confirmed headcount.
    /// </summary>
    Task<UnderstaffedShiftInfo?> GetUnderstaffedShiftAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds active staff members matching the shift's required role and currency for any required skill.
    /// </summary>
    Task<IReadOnlyList<StaffCandidate>> FindEligibleStaffAsync(
        Guid shiftId,
        IReadOnlyList<Guid>? excludeStaffIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Filters eligible candidates down to those who are completely free (no approved leave, no overlapping shifts).
    /// </summary>
    Task<IReadOnlyList<StaffCandidate>> FindFreeStaffAsync(
        IReadOnlyCollection<StaffCandidate> candidates,
        Shift shift,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for cross-ward cascading swap candidates in donor wards operating above their minimum headcount.
    /// </summary>
    Task<IReadOnlyList<CascadingSwapCandidate>> FindCascadingSwapCandidatesAsync(
        Shift targetShift,
        IReadOnlyList<Guid>? excludeStaffIds = null,
        IReadOnlyList<Guid>? excludeWardIds = null,
        CancellationToken cancellationToken = default);
}
