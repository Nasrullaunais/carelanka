using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

public class IllegalTransitionException : ConflictException
{
    public IllegalTransitionException(string entityType, string from, string to)
        : base(MessageCode.IllegalTransition, entityType, from, to)
    {
    }
}
