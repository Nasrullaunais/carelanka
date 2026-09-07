using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

/// <summary>400. For a rule that model validation cannot express.</summary>
public class BadRequestException : ApiException
{
    public BadRequestException(MessageCode code, params object?[] args)
        : base(HttpStatusCode.BadRequest, code, args)
    {
    }
}
