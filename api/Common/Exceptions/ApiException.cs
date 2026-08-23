using System.Net;
using CareLanka.Api.Common.Messages;

namespace CareLanka.Api.Common.Exceptions;

/// <summary>
/// Base for every error the API deliberately returns. Throw one of the subclasses from a
/// facade; the central exception handler turns it into a ProblemDetails at the right
/// status. Never catch these in a controller — a controller that reshapes an error
/// produces a response the generated clients cannot classify.
/// </summary>
public abstract class ApiException(HttpStatusCode status, MessageCode code, params object?[] args)
    : Exception(MessageCatalog.Format(code, args))
{
    public HttpStatusCode Status { get; } = status;
    public MessageCode Code { get; } = code;
    public object?[] Args { get; } = args;
}

/// <summary>404. Thrown by the facade when a service returned null.</summary>
public class NotFoundException(MessageCode code, params object?[] args)
    : ApiException(HttpStatusCode.NotFound, code, args)
{
    public NotFoundException(string resource, object id)
        : this(MessageCode.cl_err_001_not_found, resource, id) { }
}

/// <summary>400. The request itself is wrong, beyond simple field validation.</summary>
public class BadRequestException(MessageCode code, params object?[] args)
    : ApiException(HttpStatusCode.BadRequest, code, args);

/// <summary>403. The caller is authenticated but not allowed to do this.</summary>
public class ForbiddenException(MessageCode code, params object?[] args)
    : ApiException(HttpStatusCode.Forbidden, code, args)
{
    public ForbiddenException() : this(MessageCode.cl_err_003_forbidden) { }
}

/// <summary>409. The request clashes with the current state of the data.</summary>
public class ConflictException(MessageCode code, params object?[] args)
    : ApiException(HttpStatusCode.Conflict, code, args);

/// <summary>
/// 409, but specifically "that move is not legal in this state machine".
/// Its own type because the specs define explicit transition matrices and document an
/// illegal move as a distinct response — clients show a different message for it.
/// </summary>
public class IllegalTransitionException(MessageCode code, params object?[] args)
    : ConflictException(code, args)
{
    public IllegalTransitionException(string entity, object id, object from, object to)
        : this(MessageCode.cl_err_005_illegal_transition, entity, id, from, to) { }
}
