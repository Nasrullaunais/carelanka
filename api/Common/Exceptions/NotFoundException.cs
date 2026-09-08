using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

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
