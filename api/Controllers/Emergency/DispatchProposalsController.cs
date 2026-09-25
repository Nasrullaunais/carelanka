using CareLanka.Api.Common.Auth;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Emergency;
using CareLanka.Api.Services.Emergency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareLanka.Api.Controllers.Emergency;

[ApiController]
[Route("api")]
[Tags("Dispatch Agent")]
[Authorize(Policy = Policies.DutyManager)]
public sealed class DispatchProposalsController : ControllerBase
{
    private readonly IDispatchProposalService _proposals;

    public DispatchProposalsController(IDispatchProposalService proposals) => _proposals = proposals;

    [HttpGet("dispatch-proposals", Name = "listDispatchProposals")]
    [ProducesResponseType(typeof(PagedResult<DispatchProposalSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<PagedResult<DispatchProposalSummary>>> List(
        [FromQuery] ListDispatchProposalsRequest request, CancellationToken ct)
        => Ok(await _proposals.ListAsync(request, ct));

    [HttpPost("dispatch-proposals", Name = "createDispatchProposal")]
    [ProducesResponseType(typeof(DispatchProposalSummary), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DispatchProposalSummary>> Create(
        [FromBody] CreateDispatchProposalRequest request, CancellationToken ct)
    {
        var accepted = await _proposals.StartAsync(request, ct);
        return AcceptedAtAction(nameof(Get), new { id = accepted.Id }, accepted);
    }

    [HttpGet("dispatch-proposals/{id:guid}", Name = "getDispatchProposal")]
    [ProducesResponseType(typeof(DispatchProposalDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<DispatchProposalDetail>> Get(Guid id, CancellationToken ct)
        => Ok(await _proposals.GetAsync(id, ct));

    [HttpPost("dispatch-proposals/{id:guid}/confirm", Name = "confirmDispatchProposal")]
    [ProducesResponseType(typeof(DispatchProposalDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DispatchProposalDetail>> Confirm(Guid id, CancellationToken ct)
        => Ok(await _proposals.ConfirmAsync(id, ct));

    [HttpPost("dispatch-proposals/{id:guid}/approve", Name = "approveDispatchProposal")]
    [ProducesResponseType(typeof(DispatchProposalDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DispatchProposalDetail>> Approve(
        Guid id, [FromBody] ApproveDispatchProposalRequest request, CancellationToken ct)
        => Ok(await _proposals.ApproveAsync(id, request, ct));

    [HttpPost("dispatch-proposals/{id:guid}/reject", Name = "rejectDispatchProposal")]
    [ProducesResponseType(typeof(DispatchProposalDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DispatchProposalDetail>> Reject(
        Guid id, [FromBody] RejectDispatchProposalRequest request, CancellationToken ct)
        => Ok(await _proposals.RejectAsync(id, request, ct));
}
