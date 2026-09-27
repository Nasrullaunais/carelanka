using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

public class TooManyRequestsException : ApiException
{
    public TooManyRequestsException(
        MessageCode code = MessageCode.TooManyRequests, params object?[] args)
        : base(HttpStatusCode.TooManyRequests, code, args)
    {
    }
}
