using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

/// <summary>409. The request clashes with something already recorded.</summary>
public class ConflictException : ApiException
{
    public ConflictException(MessageCode code = MessageCode.Conflict, params object?[] args)
        : base(HttpStatusCode.Conflict, code, args)
    {
    }
}
