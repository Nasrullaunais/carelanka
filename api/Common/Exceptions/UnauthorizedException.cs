using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

public class UnauthorizedException : ApiException
{
    public UnauthorizedException(MessageCode code = MessageCode.InvalidCredentials, params object?[] args)
        : base(HttpStatusCode.Unauthorized, code, args)
    {
    }
}
