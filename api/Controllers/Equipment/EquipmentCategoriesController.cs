using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

/// <summary>Equipment categories. A table rather than an enum, so a sixth can be added without a migration.</summary>
[ApiController]
[Route("api/equipment-categories")]
[Tags("Equipment")]
public class EquipmentCategoriesController : ControllerBase
{
    private readonly IEquipmentCategoryService _categories;

    public EquipmentCategoriesController(IEquipmentCategoryService categories)
        => _categories = categories;

    /// <summary>List the equipment categories. Any staff member may read them, because anyone browsing equipment needs them.</summary>
    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet(Name = "listEquipmentCategories")]
    [ProducesResponseType(typeof(IReadOnlyList<EquipmentCategory>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<EquipmentCategory>>> ListEquipmentCategories(
        CancellationToken ct)
        => Ok(await _categories.ListAsync(ct));

    /// <summary>Add a category. Names are compared without case, so "Surgical Gear" and "surgical gear" cannot both exist.</summary>
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

        // No Location header: the contract publishes no endpoint that reads one category.
        return Created((string?)null, category);
    }
}
