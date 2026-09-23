using CareLanka.Api.Common.Errors;
using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Common.Exceptions;

public sealed class StaffDeactivationConflictException : ConflictException
{
    public StaffDeactivationConflictException(
        IReadOnlyList<AllocationSummaryDto> affectedAllocations,
        string? customMessage = null)
        : base(MessageCode.Conflict)
    {
        AffectedAllocations = affectedAllocations;
        CustomMessage = customMessage;
    }

    public IReadOnlyList<AllocationSummaryDto> AffectedAllocations { get; }

    public string? CustomMessage { get; }
}
