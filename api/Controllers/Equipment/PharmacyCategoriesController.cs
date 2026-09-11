using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Equipment;
using CareLanka.Api.Services.Equipment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Equipment;

/// <summary>Pharmacy categories. A table rather than an enum, so a sixth can be added without a migration.</summary>
[ApiController]
[Route("api/pharmacy-categories")]
[Tags("Pharmacy")]
public class PharmacyCategoriesController : ControllerBase
{
    private readonly IPharmacyCategoryService _categories;

    public PharmacyCategoriesController(IPharmacyCategoryService categories)
        => _categories = categories;

    /// <summary>List the pharmacy categories. Any staff member may read them, because anyone searching for a medicine needs them.</summary>
    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet(Name = "listPharmacyCategories")]
    [ProducesResponseType(typeof(IReadOnlyList<PharmacyCategory>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<PharmacyCategory>>> ListPharmacyCategories(
        CancellationToken ct)
        => Ok(await _categories.ListAsync(ct));

    /// <summary>Add a category. Names are compared without case, so one category cannot exist twice under different capitalisation.</summary>
    [Authorize(Policy = Policies.EquipmentManager)]
    [HttpPost(Name = "createPharmacyCategory")]
    [ProducesResponseType(typeof(PharmacyCategory), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<PharmacyCategory>> CreatePharmacyCategory(
        [FromBody] CreatePharmacyCategoryRequest request, CancellationToken ct)
    {
        var category = await _categories.CreateAsync(request, ct);

        // No Location header: the contract publishes no endpoint that reads one category.
        return Created((string?)null, category);
    }
}
