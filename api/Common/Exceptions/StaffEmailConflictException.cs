using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

public sealed class StaffEmailConflictException : ConflictException
{
    public StaffEmailConflictException(Guid? existingId = null, string? customMessage = null)
        : base(MessageCode.Conflict)
    {
        ExistingId = existingId;
        CustomMessage = customMessage;
    }

    public Guid? ExistingId { get; }

    public string? CustomMessage { get; }
}
