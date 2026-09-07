using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

/// <summary>
/// 409, and its own type rather than a plain conflict because the specs define explicit
/// state machines and document an illegal move as a distinct response. A client can tell
/// "you cannot do that from here" apart from "someone else got there first".
/// </summary>
public class IllegalTransitionException : ConflictException
{
    public IllegalTransitionException(string entityType, string from, string to)
        : base(MessageCode.IllegalTransition, entityType, from, to)
    {
    }
}
