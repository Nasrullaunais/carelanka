using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

public class TooManyRequestsException : ApiException
{
    public TooManyRequestsException()
        : base(HttpStatusCode.TooManyRequests, MessageCode.TooManyRequests)
    {
    }
}
