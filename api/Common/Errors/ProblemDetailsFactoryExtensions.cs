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

        if (exception is DispatchProposalRejectedException dispatchRejected)
        {
            if (dispatchRejected.BlockReason is { } blockReason)
            {
                problem.Extensions["block_reason"] = CareLanka.Api.Common.Persistence.EnumWire.ToWire(blockReason);
            }

            problem.Extensions["failed_checks"] = dispatchRejected.FailedChecks;
        }
        else if (exception is StaffEmailConflictException emailConflict)
        {
            if (emailConflict.ExistingId.HasValue)
            {
                problem.Extensions["existing_id"] = emailConflict.ExistingId.Value;
            }

            if (!string.IsNullOrWhiteSpace(emailConflict.CustomMessage))
            {
                problem.Detail = emailConflict.CustomMessage;
            }
        }
        else if (exception is StaffDeactivationConflictException deactivationConflict)
        {
            problem.Extensions["affected_allocations"] = deactivationConflict.AffectedAllocations;

            if (!string.IsNullOrWhiteSpace(deactivationConflict.CustomMessage))
            {
                problem.Detail = deactivationConflict.CustomMessage;
            }
        }
        else if (exception is InvalidSkillsBadRequestException invalidSkills)
        {
            problem.Extensions["invalid_skill_ids"] = invalidSkills.InvalidSkillIds;

            if (!string.IsNullOrWhiteSpace(invalidSkills.CustomMessage))
            {
                problem.Detail = invalidSkills.CustomMessage;
            }
        }

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
