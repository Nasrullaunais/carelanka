using CareLanka.Api.Common.Auth;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Staff;

[ApiController]
[Route("api/skills")]
[Tags("Skills")]
public sealed class SkillsController : ControllerBase
{
    private readonly ISkillService _skills;
    private readonly ICurrentUser _currentUser;

    public SkillsController(ISkillService skills, ICurrentUser currentUser)
    {
        _skills = skills;
        _currentUser = currentUser;
    }

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet(Name = "listSkills")]
    [ProducesResponseType(typeof(IReadOnlyList<SkillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<SkillDto>>> ListSkills(
        [FromQuery] string? search,
        CancellationToken ct = default)
        => Ok(await _skills.ListSkillsAsync(search, ct));

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPost(Name = "createSkill")]
    [ProducesResponseType(typeof(SkillDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SkillDto>> CreateSkill(
        [FromBody] CreateSkillRequest request,
        CancellationToken ct = default)
    {
        var skill = await _skills.CreateSkillAsync(request, ct);
        return Created((string?)null, skill);
    }

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPut("{id:guid}", Name = "updateSkill")]
    [ProducesResponseType(typeof(SkillDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<SkillDto>> UpdateSkill(
        Guid id,
        [FromBody] UpdateSkillRequest request,
        CancellationToken ct = default)
        => Ok(await _skills.UpdateSkillAsync(id, request, ct));

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpDelete("{id:guid}", Name = "retireSkill")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> RetireSkill(Guid id, CancellationToken ct = default)
    {
        await _skills.RetireSkillAsync(id, ct);
        return NoContent();
    }

    [Authorize(Policy = Policies.AnyStaff)]
    [HttpGet("/api/staff/{id:guid}/skills", Name = "listStaffSkills")]
    [ProducesResponseType(typeof(IReadOnlyList<StaffSkillDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<StaffSkillDto>>> ListStaffSkills(
        Guid id,
        CancellationToken ct = default)
    {
        if (_currentUser.Role != PrincipalRole.HospitalAdministrator &&
            _currentUser.Role != PrincipalRole.DutyManager &&
            _currentUser.Id != id)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }

        return Ok(await _skills.ListStaffSkillsAsync(id, ct));
    }

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPost("/api/staff/{id:guid}/skills", Name = "grantStaffSkill")]
    [ProducesResponseType(typeof(StaffSkillDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<StaffSkillDto>> GrantStaffSkill(
        Guid id,
        [FromBody] GrantStaffSkillRequest request,
        CancellationToken ct = default)
    {
        var staffSkill = await _skills.GrantStaffSkillAsync(id, request, ct);
        return Created((string?)null, staffSkill);
    }

    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpDelete("/api/staff/{staffId:guid}/skills/{skillId:guid}", Name = "revokeStaffSkill")]
    [ProducesResponseType(typeof(RevokeStaffSkillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RevokeStaffSkillResponse>> RevokeStaffSkill(
        Guid staffId,
        Guid skillId,
        CancellationToken ct = default)
        => Ok(await _skills.RevokeStaffSkillAsync(staffId, skillId, ct));
}
