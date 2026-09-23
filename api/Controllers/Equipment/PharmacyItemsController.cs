using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

[ApiController]
[Route("api/pharmacy-items")]
[Tags("Pharmacy")]
public class PharmacyItemsController : ControllerBase
{
    private readonly IPharmacyItemService _items;
    private readonly IReorderSuggestionService _reorderSuggestions;

    public PharmacyItemsController(IPharmacyItemService items, IReorderSuggestionService reorderSuggestions)
    {
        _items = items;
        _reorderSuggestions = reorderSuggestions;
    }

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

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("{id:guid}", Name = "getPharmacyItem")]
    [ProducesResponseType(typeof(PharmacyItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PharmacyItem>> GetPharmacyItem(Guid id, CancellationToken ct)
        => Ok(await _items.GetAsync(id, ct));

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

    [Authorize(Policy = Policies.PharmacyRemover)]
    [HttpDelete("{id:guid}", Name = "removePharmacyItem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> RemovePharmacyItem(
        Guid id,
        [FromHeader(Name = EquipmentOptions.ConfirmationCodeHeader)] string? confirmationCode,
        CancellationToken ct)
    {
        await _items.RemoveAsync(id, confirmationCode, ct);

        return NoContent();
    }

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("{id:guid}/batches", Name = "listPharmacyBatches")]
    [ProducesResponseType(typeof(List<PharmacyBatch>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<PharmacyBatch>>> ListPharmacyBatches(
        Guid id, CancellationToken ct)
        => Ok(await _items.ListBatchesAsync(id, ct));

    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost("{id:guid}/batches", Name = "addPharmacyBatch")]
    [ProducesResponseType(typeof(PharmacyBatch), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PharmacyBatch>> AddPharmacyBatch(
        Guid id, [FromBody] AddPharmacyBatchRequest request, CancellationToken ct)
    {
        var batch = await _items.AddBatchAsync(id, request, ct);

        return CreatedAtRoute("listPharmacyBatches", new { id }, batch);
    }

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

        return Created((string?)null, item);
    }

    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost("{id:guid}/batches/{batchId:guid}/transactions", Name = "recordPharmacyBatchTransaction")]
    [ProducesResponseType(typeof(PharmacyItem), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<PharmacyItem>> RecordPharmacyBatchTransaction(
        Guid id,
        Guid batchId,
        [FromBody] CreatePharmacyTransactionRequest request,
        CancellationToken ct)
    {
        var item = await _items.RecordBatchTransactionAsync(id, batchId, request, ct);

        return Created((string?)null, item);
    }

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

    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPatch("{id:guid}/reorder-threshold", Name = "updateReorderThreshold")]
    [ProducesResponseType(typeof(PharmacyItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PharmacyItem>> UpdateReorderThreshold(
        Guid id, [FromBody] UpdateReorderThresholdRequest request, CancellationToken ct)
        => Ok(await _items.UpdateReorderThresholdAsync(id, request.ReorderThreshold, ct));

    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost("{id:guid}/reorder-suggestion", Name = "submitReorderSuggestion")]
    [ProducesResponseType(typeof(ReorderSuggestionAccepted), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ReorderSuggestionAccepted>> SubmitReorderSuggestion(
        Guid id, CancellationToken ct)
    {
        var accepted = await _reorderSuggestions.SubmitAsync(id, ct);

        return Accepted(accepted.PollUrl, accepted);
    }

    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpGet("reorder-suggestions/{workflowId:guid}", Name = "getReorderSuggestionWorkflow")]
    [ProducesResponseType(typeof(ReorderWorkflowSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ReorderWorkflowSummary>> GetReorderSuggestionWorkflow(
        Guid workflowId, CancellationToken ct)
        => Ok(await _reorderSuggestions.GetWorkflowAsync(workflowId, ct));
}
