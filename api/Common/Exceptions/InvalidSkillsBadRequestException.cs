using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

public sealed class InvalidSkillsBadRequestException : BadRequestException
{
    public InvalidSkillsBadRequestException(
        IReadOnlyList<Guid> invalidSkillIds,
        string? customMessage = null)
        : base(MessageCode.ValidationFailed)
    {
        InvalidSkillIds = invalidSkillIds;
        CustomMessage = customMessage;
    }

    public IReadOnlyList<Guid> InvalidSkillIds { get; }

    public string? CustomMessage { get; }
}
