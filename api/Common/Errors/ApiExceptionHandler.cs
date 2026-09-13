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
        switch (exception)
        {
            case IllegalTransitionException illegalTransitionException:
                await HandleApiExceptionAsync(context, illegalTransitionException, cancellationToken);
                return true;

            case ApiException apiException:
                await HandleApiExceptionAsync(context, apiException, cancellationToken);
                return true;

            default:
                _logger.LogError(
                    exception, "Unhandled exception on {Method} {Path}",
                    context.Request.Method, context.Request.Path);

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal Server Error",
                    Detail = MessageCode.Unexpected.ToText()
                }.WithCareLankaExtensions(context, MessageCode.Unexpected);

                await ProblemResponseWriter.WriteAsync(context, problem, cancellationToken);
                return true;
        }
    }

    private async Task HandleApiExceptionAsync(
        HttpContext context, ApiException exception, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "{Status} {Code} on {Method} {Path}: {Message}",
            exception.Status, exception.Code.ToWire(),
            context.Request.Method, context.Request.Path, exception.Message);

        await ProblemResponseWriter.WriteAsync(
            context, exception.ToProblemDetails(context), cancellationToken);
    }
}
