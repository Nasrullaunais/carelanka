using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Common;

[ApiController]
[Route("api/notifications")]
[Tags("Notifications")]
[Authorize(Policy = Policies.AnyPrincipal)]
public sealed class NotificationsController(IInboxService inbox) : ControllerBase
{
    [HttpGet(Name = "listMyNotifications")]
    [ProducesResponseType(typeof(PagedResult<InboxNotification>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<InboxNotification>>> ListMyNotifications(
        [FromQuery] ListMyNotificationsQueryParameters parameters, CancellationToken cancellationToken)
        => Ok(await inbox.ListMyNotificationsAsync(parameters, cancellationToken));

    [HttpGet("unread-count", Name = "getMyUnreadNotificationCount")]
    [ProducesResponseType(typeof(UnreadNotificationCount), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<UnreadNotificationCount>> GetMyUnreadCount(CancellationToken cancellationToken)
        => Ok(await inbox.GetMyUnreadCountAsync(cancellationToken));

    [HttpPost("{id:guid}/read", Name = "markNotificationRead")]
    [ProducesResponseType(typeof(InboxNotification), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<InboxNotification>> MarkRead(Guid id, CancellationToken cancellationToken)
        => Ok(await inbox.MarkReadAsync(id, cancellationToken));

    [HttpPost("read-all", Name = "markAllNotificationsRead")]
    [ProducesResponseType(typeof(UnreadNotificationCount), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<UnreadNotificationCount>> MarkAllRead(CancellationToken cancellationToken)
        => Ok(await inbox.MarkAllReadAsync(cancellationToken));
}
