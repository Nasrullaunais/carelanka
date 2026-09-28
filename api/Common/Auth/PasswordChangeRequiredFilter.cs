using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CareLanka.Api.Common.Auth;

public sealed class PasswordChangeRequiredFilter : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var mustChange = context.HttpContext.User.HasClaim(CareLankaClaims.MustChangePassword, "true");
        var allowed = context.ActionDescriptor.EndpointMetadata.OfType<AllowWhilePasswordChangeRequiredAttribute>().Any();

        if (mustChange && !allowed)
        {
            throw new ForbiddenException(MessageCode.PasswordChangeRequired);
        }

        return Task.CompletedTask;
    }
}
