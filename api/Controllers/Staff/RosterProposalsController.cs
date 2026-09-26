using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Staff;

[ApiController]
[Route("api/roster-proposals")]
[Tags("Roster Agent")]
public sealed class RosterProposalsController : ControllerBase
{
    private readonly IRosterProposalService _rosterProposalService;

    public RosterProposalsController(IRosterProposalService rosterProposalService)
    {
        _rosterProposalService = rosterProposalService;
    }

    /// <summary>
    /// List roster proposals with optional filtering by status, ward, shift, and pagination.
    /// Roles: HospitalAdministrator, DutyManager.
    /// </summary>
    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpGet(Name = "listRosterProposals")]
    [ProducesResponseType(typeof(PagedResult<RosterProposalSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<RosterProposalSummary>>> ListRosterProposals(
        [FromQuery] ListRosterProposalsQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await _rosterProposalService.ListProposalsAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Ask the Staff Allocation Agent to fill a gap for a shift.
    /// Roles: HospitalAdministrator, DutyManager.
    /// </summary>
    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpPost(Name = "createRosterProposal")]
    [ProducesResponseType(typeof(RosterProposalSummary), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RosterProposalSummary>> CreateRosterProposal(
        [FromBody] CreateRosterProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _rosterProposalService.CreateProposalAsync(request, cancellationToken);
        return AcceptedAtAction(nameof(GetRosterProposal), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get a proposal with its plan, validation results, tool calls, and proposed changes.
    /// Roles: HospitalAdministrator, DutyManager.
    /// </summary>
    [Authorize(Roles = "hospital_administrator,duty_manager")]
    [HttpGet("{id:guid}", Name = "getRosterProposal")]
    [ProducesResponseType(typeof(RosterProposalDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RosterProposalDetail>> GetRosterProposal(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await _rosterProposalService.GetProposalDetailAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Approve a roster proposal and apply every proposed change atomically.
    /// Roles: HospitalAdministrator.
    /// </summary>
    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPost("{id:guid}/approve", Name = "approveRosterProposal")]
    [ProducesResponseType(typeof(RosterProposalDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RosterProposalDetail>> ApproveRosterProposal(
        [FromRoute] Guid id,
        [FromBody] ApproveRosterProposalRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _rosterProposalService.ApproveProposalAsync(
            id,
            request ?? new ApproveRosterProposalRequest(),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Reject a roster proposal with a recorded reason.
    /// Roles: HospitalAdministrator.
    /// </summary>
    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPost("{id:guid}/reject", Name = "rejectRosterProposal")]
    [ProducesResponseType(typeof(RosterProposalDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RosterProposalDetail>> RejectRosterProposal(
        [FromRoute] Guid id,
        [FromBody] RejectRosterProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _rosterProposalService.RejectProposalAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Send a proposal back for revision with reviewer guidance and exclusion constraints.
    /// Roles: HospitalAdministrator.
    /// </summary>
    [Authorize(Policy = Policies.HospitalAdministrator)]
    [HttpPost("{id:guid}/request-revision", Name = "reviseRosterProposal")]
    [ProducesResponseType(typeof(RosterProposalSummary), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RosterProposalSummary>> ReviseRosterProposal(
        [FromRoute] Guid id,
        [FromBody] RequestRosterProposalRevisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _rosterProposalService.RequestRevisionAsync(id, request, cancellationToken);
        return AcceptedAtAction(nameof(GetRosterProposal), new { id = result.Id }, result);
    }
}
