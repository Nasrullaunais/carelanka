using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

public class ForbiddenException : ApiException
{
    public ForbiddenException(MessageCode code = MessageCode.Forbidden, params object?[] args)
        : base(HttpStatusCode.Forbidden, code, args)
    {
    }
}
