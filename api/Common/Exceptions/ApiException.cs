using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

public abstract class ApiException : Exception
{
    protected ApiException(HttpStatusCode status, MessageCode code, params object?[] args)
        : base(code.ToText(args))
    {
        Status = (int)status;
        Code = code;
        MessageArgs = args;
    }

    public int Status { get; }

    public MessageCode Code { get; }

    public object?[] MessageArgs { get; }
}
