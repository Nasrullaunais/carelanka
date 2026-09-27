using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Staff;

[ApiController]
[Route("api/allocations")]
[Tags("Allocations")]
public sealed class AllocationsController : ControllerBase
{
    private readonly IAllocationService _allocationService;

    public AllocationsController(IAllocationService allocationService)
    {
        _allocationService = allocationService;
    }

    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpGet(Name = "listAllocations")]
    [ProducesResponseType(typeof(PagedResult<AllocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<AllocationDto>>> ListAllocations(
        [FromQuery] ListAllocationsQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await _allocationService.ListAllocationsAsync(parameters, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpPost(Name = "createAllocation")]
    [ProducesResponseType(typeof(AllocationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AllocationDto>> CreateAllocation(
        [FromBody] CreateAllocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _allocationService.CreateAllocationAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpPost("{id:guid}/end", Name = "endAllocation")]
    [ProducesResponseType(typeof(EndAllocationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EndAllocationResponse>> EndAllocation(
        [FromRoute] Guid id,
        [FromBody] EndAllocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _allocationService.EndAllocationAsync(id, request, cancellationToken);
        return Ok(result);
    }
}
