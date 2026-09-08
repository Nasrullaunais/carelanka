using CareLanka.Api.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Common.Errors;

public static class ProblemDetailsFactoryExtensions
{
    public const string TraceIdHeader = "X-Trace-Id";

    public static ProblemDetails WithCareLankaExtensions(
        this ProblemDetails problem, HttpContext context, MessageCode code)
    {
        var traceId = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;

        problem.Extensions["code"] = code.ToWire();
        problem.Extensions["traceId"] = traceId;
        problem.Instance ??= context.Request.Path;

        if (!context.Response.HasStarted)
        {
            context.Response.Headers[TraceIdHeader] = traceId;
        }

        return problem;
    }

    public static ProblemDetails ToProblemDetails(this ApiException exception, HttpContext context)
    {
        var problem = new ProblemDetails
        {
            Status = exception.Status,
            Title = ReasonPhrase(exception.Status),
            Detail = exception.Message
        };

        return problem.WithCareLankaExtensions(context, exception.Code);
    }

    private static string ReasonPhrase(int status) => status switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        429 => "Too Many Requests",
        _ => "Error"
    };
}
