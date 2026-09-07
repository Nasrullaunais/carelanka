using CareLanka.Api.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Common.Errors;

/// <summary>
/// The single exception handler for the whole application.
/// <para>
/// A known <see cref="ApiException"/> becomes a <c>ProblemDetails</c> at its own status.
/// Anything else becomes a 500 with a generic message and <strong>no internal
/// detail</strong> — a stack trace in a response body is a security finding, and the trace
/// id is how a developer finds the real error in the logs instead.
/// </para>
/// <para>
/// Logging is split on purpose: <c>Error</c> for 5xx, <c>Information</c> for 4xx. A 404 is
/// not an incident, and treating it as one buries the real ones.
/// </para>
/// </summary>
public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;

        if (exception is ApiException apiException)
        {
            _logger.LogInformation(
                "{Status} {Code} on {Method} {Path}: {Message}",
                apiException.Status, apiException.Code.ToWire(),
                context.Request.Method, context.Request.Path, apiException.Message);

            problem = apiException.ToProblemDetails(context);
        }
        else
        {
            _logger.LogError(
                exception, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = MessageCode.Unexpected.ToText()
            }.WithCareLankaExtensions(context, MessageCode.Unexpected);
        }

        context.Response.StatusCode = problem.Status!.Value;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
