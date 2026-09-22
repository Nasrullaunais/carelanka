using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

[ApiController]
[Route("api/equipment-categories")]
[Tags("Equipment")]
public class EquipmentCategoriesController : ControllerBase
{
    private readonly IEquipmentCategoryService _categories;

    public EquipmentCategoriesController(IEquipmentCategoryService categories)
        => _categories = categories;

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet(Name = "listEquipmentCategories")]
    [ProducesResponseType(typeof(IReadOnlyList<EquipmentCategory>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<EquipmentCategory>>> ListEquipmentCategories(
        CancellationToken ct)
        => Ok(await _categories.ListAsync(ct));

    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost(Name = "createEquipmentCategory")]
    [ProducesResponseType(typeof(EquipmentCategory), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EquipmentCategory>> CreateEquipmentCategory(
        [FromBody] CreateEquipmentCategoryRequest request, CancellationToken ct)
    {
        var category = await _categories.CreateAsync(request, ct);

        return Created((string?)null, category);
    }

    // Tidying the category list is the hospital administrator's, with the confirmation code,
    // the same as confirming and retiring equipment.
    [Authorize(Policy = Policies.EquipmentConfirmer)]
    [HttpGet("for-removal", Name = "listEquipmentCategoriesForRemoval")]
    [ProducesResponseType(typeof(List<EquipmentCategoryUsage>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<EquipmentCategoryUsage>>> ListEquipmentCategoriesForRemoval(
        [FromHeader(Name = EquipmentOptions.ConfirmationCodeHeader)] string? confirmationCode,
        CancellationToken ct)
        => Ok(await _categories.ListForRemovalAsync(confirmationCode, ct));

    [Authorize(Policy = Policies.EquipmentConfirmer)]
    [HttpDelete("{id:guid}", Name = "removeEquipmentCategory")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> RemoveEquipmentCategory(
        Guid id,
        [FromHeader(Name = EquipmentOptions.ConfirmationCodeHeader)] string? confirmationCode,
        CancellationToken ct)
    {
        await _categories.RemoveAsync(id, confirmationCode, ct);

        return NoContent();
    }
}
