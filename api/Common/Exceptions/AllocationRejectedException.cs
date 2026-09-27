using CareLanka.Api.Common.Errors;
using CareLanka.Api.DTOs.Staff;

namespace CareLanka.Api.Common.Exceptions;

public sealed class AllocationRejectedException : ConflictException
{
    public AllocationRejectedException(
        IReadOnlyList<RosterValidationResult> failedChecks,
        string? customMessage = null)
        : base(MessageCode.Conflict)
    {
        FailedChecks = failedChecks;
        CustomMessage = customMessage;
    }

    public IReadOnlyList<RosterValidationResult> FailedChecks { get; }

    public string? CustomMessage { get; }
}