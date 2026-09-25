using CareLanka.Api.Common.Errors;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Common.Exceptions;

public sealed class DispatchProposalRejectedException : ConflictException
{
    public DispatchProposalRejectedException(
        MessageCode code, DiversionBlockReason? blockReason, IReadOnlyList<string> failedChecks, params object?[] args)
        : base(code, args)
    {
        BlockReason = blockReason;
        FailedChecks = failedChecks;
    }

    public DiversionBlockReason? BlockReason { get; }

    public IReadOnlyList<string> FailedChecks { get; }
}
