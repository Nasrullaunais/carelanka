using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Staff;

[ApiController]
[Route("api/staff")]
[Tags("Staff")]
public sealed class StaffController : ControllerBase
{
    private readonly IStaffLookupService _staffLookup;
    private readonly IStaffMemberService _staffService;

    public StaffController(
        IStaffLookupService staffLookup,
        IStaffMemberService staffService)
    {
        _staffLookup = staffLookup;
        _staffService = staffService;
    }

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPost(Name = "createStaffMember")]
    [ProducesResponseType(typeof(StaffMemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<StaffMemberDto>> CreateStaffMember(
        [FromBody] CreateStaffMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _staffService.CreateStaffMemberAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetStaffMember), new { id = result.Id }, result);
    }

    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpGet(Name = "listStaff")]
    [ProducesResponseType(typeof(PagedResult<StaffSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<StaffSummaryDto>>> ListStaff(
        [FromQuery] ListStaffQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await _staffService.ListStaffAsync(parameters, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpGet("{id:guid}", Name = "getStaffMember")]
    [ProducesResponseType(typeof(StaffMemberDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<StaffMemberDetailDto>> GetStaffMember(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _staffService.GetStaffMemberAsync(id, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPut("{id:guid}", Name = "updateStaffMember")]
    [ProducesResponseType(typeof(UpdateStaffMemberResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<UpdateStaffMemberResponse>> UpdateStaffMember(
        [FromRoute] Guid id,
        [FromBody] UpdateStaffMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _staffService.UpdateStaffMemberAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPost("{id:guid}/deactivate", Name = "deactivateStaffMember")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> DeactivateStaffMember(
        [FromRoute] Guid id,
        [FromBody] DeactivateStaffMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        await _staffService.DeactivateStaffMemberAsync(id, request, cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPost("{id:guid}/reactivate", Name = "reactivateStaffMember")]
    [ProducesResponseType(typeof(StaffMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<StaffMemberDto>> ReactivateStaffMember(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _staffService.ReactivateStaffMemberAsync(id, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpPost("lookup", Name = "lookupStaff")]
    [Tags("Integration")]
    [ProducesResponseType(typeof(IReadOnlyList<StaffLookupResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<StaffLookupResult>>> LookupStaff(
        [FromBody] LookupStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.StaffIds == null)
        {
            return BadRequest();
        }

        var results = await _staffLookup.LookupAsync(request.StaffIds, cancellationToken);
        return Ok(results);
    }
}
