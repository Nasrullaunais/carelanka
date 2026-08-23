using System.Diagnostics;
using System.Net;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Common.Messages;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace CareLanka.Api.Common.Errors;

/// <summary>
/// The only place an exception becomes a response. Every error leaves the API as
/// application/problem+json, which is what the generated clients are typed against.
///
/// ApiException -> ProblemDetails at its own status, with the message code attached.
/// Anything else -> 500 with a generic message and NO internal detail. A stack trace in a
/// response body is a security problem, so the trace id is the only handle the caller
/// gets; it matches the one in the log.
/// </summary>
public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception exception, CancellationToken ct)
    {
        var traceId = Activity.Current?.Id ?? http.TraceIdentifier;

        var (status, code, detail) = exception switch
        {
            ApiException api => ((int)api.Status, api.Code, api.Message),
            OperationCanceledException => (499, MessageCode.cl_err_000_unexpected, "Request cancelled."),
            _ => ((int)HttpStatusCode.InternalServerError,
                  MessageCode.cl_err_000_unexpected,
                  MessageCatalog.Format(MessageCode.cl_err_000_unexpected))
        };

        // A 404 is not an incident. Only 5xx gets logged as an error.
        if (status >= 500)
            logger.LogError(exception, "Unhandled exception. traceId={TraceId}", traceId);
        else
            logger.LogInformation("{Code} -> {Status}. traceId={TraceId}", code, status, traceId);

        http.Response.StatusCode = status;
        http.Response.Headers["X-Trace-Id"] = traceId;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = TitleFor(status),
                Detail = detail,
                Instance = $"{http.Request.Method} {http.Request.Path}",
                Extensions =
                {
                    ["code"] = code.ToString(),
                    ["traceId"] = traceId
                }
            }
        });
    }

    private static string TitleFor(int status) => status switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        499 => "Client Closed Request",
        _ => "Server Error"
    };
}
