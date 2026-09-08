using CareLanka.Api.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Common.Errors;

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
            // Information, not Error: a 404 is not an incident, and logging it as one buries the real ones.
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

        await ProblemResponseWriter.WriteAsync(context, problem, cancellationToken);

        return true;
    }
}
