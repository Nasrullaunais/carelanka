using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

/// <summary>
/// 429. Thrown by the per-account throttle on login and registration; the per-IP limit is
/// enforced by middleware before a request ever reaches a controller.
/// </summary>
public class TooManyRequestsException : ApiException
{
    public TooManyRequestsException()
        : base(HttpStatusCode.TooManyRequests, MessageCode.TooManyRequests)
    {
    }
}
