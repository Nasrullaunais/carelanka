using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

/// <summary>
/// 404. Thrown by <c>Get*</c> service methods; <c>Find*</c> returns <c>null</c> instead.
/// Getting that pair the wrong way round means "not found" starts meaning two different
/// things at two layers.
/// </summary>
public class NotFoundException : ApiException
{
    public NotFoundException(string entityType, object id)
        : base(HttpStatusCode.NotFound, MessageCode.NotFound, entityType, id)
    {
    }

    public NotFoundException(MessageCode code, params object?[] args)
        : base(HttpStatusCode.NotFound, code, args)
    {
    }
}
