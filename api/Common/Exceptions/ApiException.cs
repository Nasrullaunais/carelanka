using System.Net;
using CareLanka.Api.Common.Errors;

namespace CareLanka.Api.Common.Exceptions;

/// <summary>
/// Anything a controller is allowed to fail with. One central handler turns these into
/// <c>application/problem+json</c>, so <strong>there is no try/catch in a controller</strong>
/// — a controller that reshapes an error produces a response the generated client cannot
/// classify.
/// </summary>
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

    /// <summary>The <c>{0}</c> parameters, kept so the text can be re-rendered in another language.</summary>
    public object?[] MessageArgs { get; }
}
