using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

[ApiController]
[Route("api/equipment-items")]
[Tags("Equipment")]
public class EquipmentItemsController : ControllerBase
{
    private readonly IEquipmentItemService _items;

    public EquipmentItemsController(IEquipmentItemService items) => _items = items;

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet(Name = "listEquipmentItems")]
    [ProducesResponseType(typeof(PagedResult<EquipmentItemSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<PagedResult<EquipmentItemSummary>>> ListEquipmentItems(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? wardId,
        [FromQuery] EquipmentStatus? status,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        [FromQuery] string sortBy = "created_at",
        [FromQuery] string sortDir = "desc",
        CancellationToken ct = default)
        => Ok(await _items.ListAsync(
            new EquipmentItemQuery(search, categoryId, wardId, status, page, pageSize, sortBy, sortDir),
            ct));

    [Authorize(Policy = Policies.EquipmentConfirmer)]
    [HttpGet("pending-confirmation", Name = "listEquipmentItemsAwaitingConfirmation")]
    [ProducesResponseType(typeof(List<EquipmentItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<EquipmentItem>>> ListEquipmentItemsAwaitingConfirmation(
        [FromHeader(Name = EquipmentOptions.ConfirmationCodeHeader)] string? confirmationCode,
        CancellationToken ct)
        => Ok(await _items.ListAwaitingConfirmationAsync(confirmationCode, ct));

    [Authorize(Policy = Policies.EquipmentConfirmationTracker)]
    [HttpGet("pending-confirmation/count", Name = "countEquipmentItemsAwaitingConfirmation")]
    [ProducesResponseType(typeof(PendingEquipmentCount), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PendingEquipmentCount>> CountEquipmentItemsAwaitingConfirmation(
        CancellationToken ct)
        => Ok(await _items.CountAwaitingConfirmationAsync(ct));

    [Authorize(Policy = Policies.EquipmentConfirmer)]
    [HttpPost("{id:guid}/confirm", Name = "confirmEquipmentItem")]
    [ProducesResponseType(typeof(EquipmentItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EquipmentItem>> ConfirmEquipmentItem(
        Guid id,
        [FromHeader(Name = EquipmentOptions.ConfirmationCodeHeader)] string? confirmationCode,
        CancellationToken ct)
        => Ok(await _items.ConfirmAsync(id, confirmationCode, ct));

    [Authorize(Policy = Policies.EquipmentConfirmer)]
    [HttpPost("{id:guid}/reject", Name = "rejectEquipmentItem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> RejectEquipmentItem(
        Guid id,
        [FromHeader(Name = EquipmentOptions.ConfirmationCodeHeader)] string? confirmationCode,
        CancellationToken ct)
    {
        await _items.RejectAsync(id, confirmationCode, ct);

        return NoContent();
    }

    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost(Name = "createEquipmentItem")]
    [ProducesResponseType(typeof(EquipmentItem), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EquipmentItem>> CreateEquipmentItem(
        [FromBody] CreateEquipmentItemRequest request, CancellationToken ct)
    {
        var item = await _items.CreateAsync(request, ct);

        return CreatedAtRoute("getEquipmentItem", new { id = item.Id }, item);
    }

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("{id:guid}", Name = "getEquipmentItem")]
    [ProducesResponseType(typeof(EquipmentItemDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<EquipmentItemDetail>> GetEquipmentItem(Guid id, CancellationToken ct)
        => Ok(await _items.GetDetailAsync(id, ct));

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("by-tag/{assetTag}", Name = "getEquipmentItemByTag")]
    [ProducesResponseType(typeof(EquipmentItemDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<EquipmentItemDetail>> GetEquipmentItemByTag(
        string assetTag, CancellationToken ct)
        => Ok(await _items.GetDetailByTagAsync(assetTag, ct));

    [Authorize(Policy = Policies.EquipmentItemEditor)]
    [HttpPut("{id:guid}", Name = "updateEquipmentItem")]
    [ProducesResponseType(typeof(EquipmentItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EquipmentItem>> UpdateEquipmentItem(
        Guid id, [FromBody] UpdateEquipmentItemRequest request, CancellationToken ct)
        => Ok(await _items.UpdateAsync(id, request, ct));

    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost("{id:guid}/assign", Name = "assignEquipmentItem")]
    [ProducesResponseType(typeof(EquipmentItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EquipmentItem>> AssignEquipmentItem(
        Guid id, [FromBody] AssignEquipmentItemRequest request, CancellationToken ct)
        => Ok(await _items.AssignAsync(id, request.AdmissionId, ct));

    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost("{id:guid}/release", Name = "releaseEquipmentItem")]
    [ProducesResponseType(typeof(EquipmentItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EquipmentItem>> ReleaseEquipmentItem(Guid id, CancellationToken ct)
        => Ok(await _items.ReleaseAsync(id, ct));

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpPost("{id:guid}/report-fault", Name = "reportEquipmentFault")]
    [ProducesResponseType(typeof(EquipmentItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EquipmentItem>> ReportEquipmentFault(
        Guid id, [FromBody] ReportFaultRequest request, CancellationToken ct)
        => Ok(await _items.ReportFaultAsync(id, request.Description, ct));
}
