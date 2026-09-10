using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

/// <summary>The equipment register: one row per physical unit, and the operations that move it through its life.</summary>
[ApiController]
[Route("api/equipment-items")]
[Tags("Equipment")]
public class EquipmentItemsController : ControllerBase
{
    private readonly IEquipmentItemService _items;

    public EquipmentItemsController(IEquipmentItemService items) => _items = items;

    /// <summary>Search equipment. Open to any staff member, because "do we have a working X" is a question anyone in the hospital may need to ask.</summary>
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

    /// <summary>Register a physical item. It starts available, and 409s if the asset tag or serial number is already in use.</summary>
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

    /// <summary>One item with its servicing history and any warnings still open.</summary>
    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("{id:guid}", Name = "getEquipmentItem")]
    [ProducesResponseType(typeof(EquipmentItemDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<EquipmentItemDetail>> GetEquipmentItem(Guid id, CancellationToken ct)
        => Ok(await _items.GetDetailAsync(id, ct));

    /// <summary>What a scanned QR tag resolves to. This is the call the Flutter scan screen makes the instant a technician scans a label.</summary>
    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("by-tag/{assetTag}", Name = "getEquipmentItemByTag")]
    [ProducesResponseType(typeof(EquipmentItemDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<EquipmentItemDetail>> GetEquipmentItemByTag(
        string assetTag, CancellationToken ct)
        => Ok(await _items.GetDetailByTagAsync(assetTag, ct));

    /// <summary>Update an item. A status change here is checked against the lifecycle, so retired stays terminal and assigned cannot be jumped into.</summary>
    [Authorize(Policy = Policies.EquipmentManager)]
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

    /// <summary>Assign an item to an admission. Only an available item can be assigned, so a ventilator cannot be given to two patients.</summary>
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

    /// <summary>Release an assigned item back to available. No assignment history is kept past this point, which the plan calls a deliberate simplification.</summary>
    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost("{id:guid}/release", Name = "releaseEquipmentItem")]
    [ProducesResponseType(typeof(EquipmentItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EquipmentItem>> ReleaseEquipmentItem(Guid id, CancellationToken ct)
        => Ok(await _items.ReleaseAsync(id, ct));

    /// <summary>Report a fault. Any staff member may, and the item moves to maintenance immediately rather than waiting for the next sweep.</summary>
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
