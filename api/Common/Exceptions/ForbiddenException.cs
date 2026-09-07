using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

/// <summary>403. Authenticated, but this role may not do this.</summary>
public class ForbiddenException : ApiException
{
    public ForbiddenException(MessageCode code = MessageCode.Forbidden, params object?[] args)
        : base(HttpStatusCode.Forbidden, code, args)
    {
    }
}
