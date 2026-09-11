using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

/// <summary>The pharmacy catalog, what is on the shelf, and every movement in or out of it.</summary>
[ApiController]
[Route("api/pharmacy-items")]
[Tags("Pharmacy")]
public class PharmacyItemsController : ControllerBase
{
    private readonly IPharmacyItemService _items;

    public PharmacyItemsController(IPharmacyItemService items) => _items = items;

    /// <summary>Search the pharmacy and check availability. Open to any staff member: "do we have this medicine" is a question anyone in the hospital may need to ask.</summary>
    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet(Name = "listPharmacyItems")]
    [ProducesResponseType(typeof(PagedResult<PharmacyItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<PagedResult<PharmacyItem>>> ListPharmacyItems(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool availableOnly = false,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        [FromQuery] string sortBy = "name",
        [FromQuery] string sortDir = "asc",
        CancellationToken ct = default)
        => Ok(await _items.ListAsync(
            new PharmacyItemQuery(search, categoryId, availableOnly, page, pageSize, sortBy, sortDir),
            ct));

    /// <summary>One catalog entry with its current quantity.</summary>
    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("{id:guid}", Name = "getPharmacyItem")]
    [ProducesResponseType(typeof(PharmacyItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PharmacyItem>> GetPharmacyItem(Guid id, CancellationToken ct)
        => Ok(await _items.GetAsync(id, ct));

    /// <summary>Add a medicine or supply to the catalog. Opening stock is set here; everything after is a transaction.</summary>
    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost(Name = "createPharmacyItem")]
    [ProducesResponseType(typeof(PharmacyItem), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<PharmacyItem>> CreatePharmacyItem(
        [FromBody] CreatePharmacyItemRequest request, CancellationToken ct)
    {
        var item = await _items.CreateAsync(request, ct);

        return CreatedAtRoute("getPharmacyItem", new { id = item.Id }, item);
    }

    /// <summary>
    /// Record a stock movement. Quantity on hand is never edited directly: every change is a
    /// transaction, applied as one conditional update, so stock cannot go negative and two
    /// people dispensing the last box at once cannot both succeed.
    /// </summary>
    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost("{id:guid}/transactions", Name = "recordPharmacyTransaction")]
    [ProducesResponseType(typeof(PharmacyItem), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<PharmacyItem>> RecordPharmacyTransaction(
        Guid id, [FromBody] CreatePharmacyTransactionRequest request, CancellationToken ct)
    {
        var item = await _items.RecordTransactionAsync(id, request, ct);

        // The item, not the transaction: what the caller wants to see is the shelf after
        // the movement. The contract publishes no endpoint that reads one transaction.
        return Created((string?)null, item);
    }

    /// <summary>One item's movement history, newest first. This is the audit trail, so nothing here is ever edited or removed.</summary>
    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpGet("{id:guid}/transactions", Name = "listPharmacyTransactions")]
    [ProducesResponseType(typeof(PagedResult<PharmacyTransaction>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PagedResult<PharmacyTransaction>>> ListPharmacyTransactions(
        Guid id,
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _items.ListTransactionsAsync(id, page, pageSize, ct));
}
