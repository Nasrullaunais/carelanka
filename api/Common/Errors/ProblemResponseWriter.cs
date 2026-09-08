using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Common.Errors;

public static class ProblemResponseWriter
{
    public const string ContentType = "application/problem+json";

    public static Task WriteAsync(
        HttpContext context,
        int status,
        string title,
        MessageCode code,
        CancellationToken cancellationToken = default)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = code.ToText()
        }.WithCareLankaExtensions(context, code);

        return WriteAsync(context, problem, cancellationToken);
    }

    public static Task WriteAsync(
        HttpContext context,
        ProblemDetails problem,
        CancellationToken cancellationToken = default)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return context.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: ContentType,
            cancellationToken: cancellationToken);
    }
}
