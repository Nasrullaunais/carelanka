using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

public class BadRequestException : ApiException
{
    public BadRequestException(MessageCode code, params object?[] args)
        : base(HttpStatusCode.BadRequest, code, args)
    {
    }
}
