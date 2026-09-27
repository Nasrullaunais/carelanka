using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Staff;

[ApiController]
[Route("api/wards/{wardId:guid}/staffing-rules")]
[Tags("Shifts")]
public sealed class WardStaffingRulesController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public WardStaffingRulesController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpGet(Name = "getWardStaffingRules")]
    [ProducesResponseType(typeof(IReadOnlyList<WardStaffingRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<WardStaffingRuleDto>>> GetWardStaffingRules(
        Guid wardId,
        CancellationToken cancellationToken = default)
    {
        var result = await _shiftService.GetWardStaffingRulesAsync(wardId, cancellationToken);
        return Ok(result);
    }

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPut(Name = "setWardStaffingRules")]
    [ProducesResponseType(typeof(ReplaceWardStaffingRulesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<ReplaceWardStaffingRulesResponse>> SetWardStaffingRules(
        Guid wardId,
        [FromBody] List<WardStaffingRuleInput> rules,
        CancellationToken cancellationToken = default)
    {
        var result = await _shiftService.ReplaceWardStaffingRulesAsync(wardId, rules, cancellationToken);
        return Ok(result);
    }
}
