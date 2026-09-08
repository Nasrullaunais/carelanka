using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

public class ConflictException : ApiException
{
    public ConflictException(MessageCode code = MessageCode.Conflict, params object?[] args)
        : base(HttpStatusCode.Conflict, code, args)
    {
    }
}
