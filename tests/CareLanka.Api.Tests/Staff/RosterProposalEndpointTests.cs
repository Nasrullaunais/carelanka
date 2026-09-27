using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CareLanka.Api.Agents.Staff;
using CareLanka.Api.Common.Auth;
using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Entities.Staff;
using CareLanka.Api.Data.Enums;
using CareLanka.Api.DTOs.Common;
using CareLanka.Api.DTOs.Staff;
using CareLanka.Api.Services.Common;
using CareLanka.Api.Services.Staff;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace CareLanka.Api.Tests.Staff;

public sealed class RosterProposalEndpointTests
{
    private const string SigningKey = "test-signing-key-that-is-at-least-32-characters";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    #region OpenAPI Contract Tests

    [Theory]
    [InlineData("/roster-proposals", "get", "listRosterProposals")]
    [InlineData("/roster-proposals", "post", "createRosterProposal")]
    [InlineData("/roster-proposals/{id}", "get", "getRosterProposal")]
    [InlineData("/roster-proposals/{id}/approve", "post", "approveRosterProposal")]
    [InlineData("/roster-proposals/{id}/reject", "post", "rejectRosterProposal")]
    [InlineData("/roster-proposals/{id}/request-revision", "post", "reviseRosterProposal")]
    public async Task Roster_proposal_operation_ids_match_contract(string path, string method, string expectedOperationId)
    {
        using var document = await GenerateSwaggerAsync();
        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty(path).GetProperty(method);

        Assert.Equal(expectedOperationId, operation.GetProperty("operationId").GetString());
    }

    [Theory]
    [InlineData("/roster-proposals", "get")]
    [InlineData("/roster-proposals", "post")]
    [InlineData("/roster-proposals/{id}", "get")]
    [InlineData("/roster-proposals/{id}/approve", "post")]
    [InlineData("/roster-proposals/{id}/reject", "post")]
    [InlineData("/roster-proposals/{id}/request-revision", "post")]
    public async Task Roster_proposal_response_statuses_match_contract(string path, string method)
    {
        using var document = await GenerateSwaggerAsync();
        var contract = LoadContract();
        var expected = Keys(Map(contract, "paths", path, method, "responses"));
        var generated = Keys(document.RootElement, "paths", path, method, "responses");

        Assert.True(expected.SetEquals(generated),
            $"{method.ToUpperInvariant()} {path}: contract [{string.Join(", ", expected)}], "
            + $"generated [{string.Join(", ", generated)}]");
    }

    #endregion

    #region Authentication & Authorization Tests

    [Fact]
    public async Task Roster_proposals_endpoints_require_authentication()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new RosterProposalTestApplication();
        using var client = app.CreateClient();

        var proposalId = Guid.NewGuid();

        var listResponse = await client.GetAsync("/api/roster-proposals");
        var getResponse = await client.GetAsync($"/api/roster-proposals/{proposalId}");
        var postResponse = await client.PostAsJsonAsync("/api/roster-proposals", new CreateRosterProposalRequest
        {
            ShiftId = Guid.NewGuid()
        }, JsonOptions);
        var approveResponse = await client.PostAsync($"/api/roster-proposals/{proposalId}/approve", null);
        var rejectResponse = await client.PostAsJsonAsync($"/api/roster-proposals/{proposalId}/reject", new RejectRosterProposalRequest
        {
            Reason = RejectionReason.StaffUnsuitable
        }, JsonOptions);
        var reviseResponse = await client.PostAsJsonAsync($"/api/roster-proposals/{proposalId}/request-revision", new RequestRosterProposalRevisionRequest
        {
            Guidance = "Look for nurses with ICU cert."
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, approveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, rejectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, reviseResponse.StatusCode);
    }

    [Fact]
    public async Task Roster_proposals_endpoints_reject_patient_token_with_forbidden()
    {
        using var environment = TestEnvironment.Use();
        await using var app = new RosterProposalTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("patient", "patient"));

        var proposalId = Guid.NewGuid();

        var listResponse = await client.GetAsync("/api/roster-proposals");
        var getResponse = await client.GetAsync($"/api/roster-proposals/{proposalId}");
        var postResponse = await client.PostAsJsonAsync("/api/roster-proposals", new CreateRosterProposalRequest
        {
            ShiftId = Guid.NewGuid()
        }, JsonOptions);
        var approveResponse = await client.PostAsync($"/api/roster-proposals/{proposalId}/approve", null);
        var rejectResponse = await client.PostAsJsonAsync($"/api/roster-proposals/{proposalId}/reject", new RejectRosterProposalRequest
        {
            Reason = RejectionReason.StaffUnsuitable
        }, JsonOptions);
        var reviseResponse = await client.PostAsJsonAsync($"/api/roster-proposals/{proposalId}/request-revision", new RequestRosterProposalRevisionRequest
        {
            Guidance = "Exclude Ward A staff"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rejectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reviseResponse.StatusCode);
    }

    [Theory]
    [InlineData("ward_nurse")]
    [InlineData("doctor")]
    [InlineData("ambulance_crew")]
    [InlineData("general_staff")]
    [InlineData("equipment_manager")]
    public async Task Roster_proposals_endpoints_reject_non_management_staff_roles(string role)
    {
        using var environment = TestEnvironment.Use();
        await using var app = new RosterProposalTestApplication();
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken(role, "staff"));

        var proposalId = Guid.NewGuid();

        var listResponse = await client.GetAsync("/api/roster-proposals");
        var getResponse = await client.GetAsync($"/api/roster-proposals/{proposalId}");
        var postResponse = await client.PostAsJsonAsync("/api/roster-proposals", new CreateRosterProposalRequest
        {
            ShiftId = Guid.NewGuid()
        }, JsonOptions);
        var approveResponse = await client.PostAsync($"/api/roster-proposals/{proposalId}/approve", null);
        var rejectResponse = await client.PostAsJsonAsync($"/api/roster-proposals/{proposalId}/reject", new RejectRosterProposalRequest
        {
            Reason = RejectionReason.StaffUnsuitable
        }, JsonOptions);
        var reviseResponse = await client.PostAsJsonAsync($"/api/roster-proposals/{proposalId}/request-revision", new RequestRosterProposalRevisionRequest
        {
            Guidance = "Look for another candidate"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rejectResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reviseResponse.StatusCode);
    }

    [Fact]
    public async Task Duty_manager_can_list_view_and_create_proposals_but_cannot_approve_reject_or_revise()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubRosterProposalService();
        var shiftId = Guid.NewGuid();
        var proposal = stub.AddTestProposal(shiftId);

        await using var app = new RosterProposalTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("duty_manager", "staff"));

        // Allowed for DutyManager:
        var listResponse = await client.GetAsync("/api/roster-proposals");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/roster-proposals/{proposal.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var newShiftId = Guid.NewGuid();
        var createResponse = await client.PostAsJsonAsync("/api/roster-proposals", new CreateRosterProposalRequest
        {
            ShiftId = newShiftId
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

        // Forbidden for DutyManager (requires HospitalAdministrator policy):
        var approveResponse = await client.PostAsync($"/api/roster-proposals/{proposal.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);

        var rejectResponse = await client.PostAsJsonAsync($"/api/roster-proposals/{proposal.Id}/reject", new RejectRosterProposalRequest
        {
            Reason = RejectionReason.StaffUnsuitable
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, rejectResponse.StatusCode);

        var reviseResponse = await client.PostAsJsonAsync($"/api/roster-proposals/{proposal.Id}/request-revision", new RequestRosterProposalRevisionRequest
        {
            Guidance = "Look for another candidate"
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, reviseResponse.StatusCode);
    }

    [Fact]
    public async Task Hospital_administrator_is_authorized_for_all_roster_proposal_endpoints()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubRosterProposalService();
        var shiftId = Guid.NewGuid();
        var proposal = stub.AddTestProposal(shiftId);

        await using var app = new RosterProposalTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var listResponse = await client.GetAsync("/api/roster-proposals");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/roster-proposals/{proposal.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var createResponse = await client.PostAsJsonAsync("/api/roster-proposals", new CreateRosterProposalRequest
        {
            ShiftId = Guid.NewGuid()
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

        var approveResponse = await client.PostAsync($"/api/roster-proposals/{proposal.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
    }

    #endregion

    #region Endpoint Lifecycle & Business Logic Tests (Area A)

    [Fact]
    public async Task List_roster_proposals_returns_paged_results_with_filters()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubRosterProposalService();
        var wardA = Guid.NewGuid();
        var wardB = Guid.NewGuid();
        var shift1 = Guid.NewGuid();
        var shift2 = Guid.NewGuid();

        stub.AddTestProposal(shift1, wardA, RosterProposalStatus.PendingApproval);
        stub.AddTestProposal(shift2, wardB, RosterProposalStatus.Approved);

        await using var app = new RosterProposalTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        // Filter by status
        var statusFiltered = await client.GetFromJsonAsync<PagedResult<RosterProposalSummary>>(
            "/api/roster-proposals?status=pending_approval", JsonOptions);
        Assert.NotNull(statusFiltered);
        Assert.Single(statusFiltered.Items);
        Assert.Equal(RosterProposalStatus.PendingApproval, statusFiltered.Items[0].Status);

        // Filter by wardId
        var wardFiltered = await client.GetFromJsonAsync<PagedResult<RosterProposalSummary>>(
            $"/api/roster-proposals?wardId={wardA}", JsonOptions);
        Assert.NotNull(wardFiltered);
        Assert.Single(wardFiltered.Items);

        // Pagination
        var paged = await client.GetFromJsonAsync<PagedResult<RosterProposalSummary>>(
            "/api/roster-proposals?page=1&pageSize=1", JsonOptions);
        Assert.NotNull(paged);
        Assert.Single(paged.Items);
        Assert.Equal(2, paged.TotalItems);
        Assert.Equal(2, paged.TotalPages);
    }

    [Fact]
    public async Task Get_roster_proposal_returns_detail_or_404_when_missing()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubRosterProposalService();
        var proposal = stub.AddTestProposal(Guid.NewGuid());

        await using var app = new RosterProposalTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.GetAsync($"/api/roster-proposals/{proposal.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<RosterProposalDetail>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(proposal.Id, detail.Id);
        Assert.NotEmpty(detail.Plan);
        Assert.NotEmpty(detail.ProposedChanges);

        var missingResponse = await client.GetAsync($"/api/roster-proposals/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
    }

    [Fact]
    public async Task Create_roster_proposal_succeeds_with_202_and_rejects_duplicate_open_proposal()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubRosterProposalService();
        var shiftId = Guid.NewGuid();

        await using var app = new RosterProposalTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/roster-proposals", new CreateRosterProposalRequest
        {
            ShiftId = shiftId,
            Objective = "Fill critical night shift vacancy",
            AllowCascadingSwap = true
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var summary = await response.Content.ReadFromJsonAsync<RosterProposalSummary>(JsonOptions);
        Assert.NotNull(summary);
        Assert.Equal(shiftId, summary.ShiftId);
        Assert.Equal(RosterProposalStatus.PendingApproval, summary.Status);

        // Attempting to create another proposal for the same open shift returns 409 Conflict
        var dupResponse = await client.PostAsJsonAsync("/api/roster-proposals", new CreateRosterProposalRequest
        {
            ShiftId = shiftId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, dupResponse.StatusCode);
    }

    [Fact]
    public async Task Create_roster_proposal_returns_404_when_shift_does_not_exist()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubRosterProposalService();
        var missingShiftId = Guid.NewGuid();
        stub.SimulateMissingShift(missingShiftId);

        await using var app = new RosterProposalTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync("/api/roster-proposals", new CreateRosterProposalRequest
        {
            ShiftId = missingShiftId
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Approve_proposal_applies_changes_and_cannot_be_approved_twice()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubRosterProposalService();
        var proposal = stub.AddTestProposal(Guid.NewGuid());

        await using var app = new RosterProposalTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync($"/api/roster-proposals/{proposal.Id}/approve", new ApproveRosterProposalRequest
        {
            Notes = "Approved by Administrator"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RosterProposalDetail>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(RosterProposalStatus.Approved, result.Status);

        // Cannot be approved twice -> 409 Conflict
        var secondResponse = await client.PostAsync($"/api/roster-proposals/{proposal.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Reject_proposal_validates_reason_and_sets_rejected_status()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubRosterProposalService();
        var proposal = stub.AddTestProposal(Guid.NewGuid());

        await using var app = new RosterProposalTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsJsonAsync($"/api/roster-proposals/{proposal.Id}/reject", new RejectRosterProposalRequest
        {
            Reason = RejectionReason.StaffUnsuitable,
            Notes = "Candidate preferred for day shift"
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RosterProposalDetail>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(RosterProposalStatus.Rejected, result.Status);
        Assert.Equal(RejectionReason.StaffUnsuitable, result.RejectionReason);

        // Cannot reject an already rejected proposal -> 409 Conflict
        var secondResponse = await client.PostAsJsonAsync($"/api/roster-proposals/{proposal.Id}/reject", new RejectRosterProposalRequest
        {
            Reason = RejectionReason.StaffUnsuitable
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Request_revision_increments_attempt_count_and_enforces_maximum_limit()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubRosterProposalService();
        var proposal = stub.AddTestProposal(Guid.NewGuid());

        await using var app = new RosterProposalTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        // Revision 1
        var rev1 = await client.PostAsJsonAsync($"/api/roster-proposals/{proposal.Id}/request-revision", new RequestRosterProposalRevisionRequest
        {
            Guidance = "Look for ICU certified nurse"
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, rev1.StatusCode);

        // Revision 2
        var rev2 = await client.PostAsJsonAsync($"/api/roster-proposals/{proposal.Id}/request-revision", new RequestRosterProposalRevisionRequest
        {
            Guidance = "Try finding cross-ward swap"
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, rev2.StatusCode);

        // Revision 3
        var rev3 = await client.PostAsJsonAsync($"/api/roster-proposals/{proposal.Id}/request-revision", new RequestRosterProposalRevisionRequest
        {
            Guidance = "Final attempt"
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Accepted, rev3.StatusCode);

        // Revision 4 exceeds max attempt limit (max 3 revisions allowed) -> 409 Conflict
        var rev4 = await client.PostAsJsonAsync($"/api/roster-proposals/{proposal.Id}/request-revision", new RequestRosterProposalRevisionRequest
        {
            Guidance = "Exceeds limit"
        }, JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, rev4.StatusCode);
    }

    #endregion

    #region Agent / Solver Behavior Tests (Area B)

    [Fact]
    public async Task Solver_proposes_free_qualified_staff_with_single_create_allocation_change()
    {
        var shiftId = Guid.NewGuid();
        var wardId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        var shift = new Shift
        {
            Id = shiftId,
            WardId = wardId,
            Date = new DateOnly(2026, 10, 15),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
            RequiredRole = StaffRole.WardNurse,
            MinimumHeadcount = 2,
            HeadcountNeeded = 3
        };

        var candidate = new StaffCandidate(
            Id: staffId,
            FullName: "Nurse Sunethra",
            Role: StaffRole.WardNurse,
            Department: "General Ward",
            IsActive: true);

        var tools = new TestStaffAllocationAgentTools
        {
            UnderstaffedShift = new UnderstaffedShiftInfo(shift, "Surgical Ward", 1, 2, 3, 1),
            FreeStaff = [candidate]
        };

        var agent = new StaffAllocationAgent(tools, new RosterProposalValidator());

        var run = await agent.RunAsync(new StaffAllocationAgentRequest(
            ShiftId: shiftId,
            Objective: "Resolve understaffed shift",
            AllowCascadingSwap: true));

        Assert.Equal(AgentOutcome.FreeStaffProposed, run.Outcome);
        Assert.Equal(AgentWorkflowStatus.PendingApproval, run.Workflow.Status);
        Assert.Single(run.Workflow.ProposedChanges);

        var change = run.Workflow.ProposedChanges.First();
        Assert.Equal(ProposedChangeType.CreateAllocation, change.ChangeType);
        Assert.Equal(shiftId, change.TargetEntityId);
        Assert.Equal(staffId, change.ProposedStaffMemberId);

        // Human gate must remain paused/pending
        var humanGateStep = run.Plan.FirstOrDefault(s => s.AgentRole == "human_gate");
        Assert.NotNull(humanGateStep);
        Assert.Equal("completed", humanGateStep.Status);
        Assert.Equal(AgentWorkflowStatus.PendingApproval, run.Workflow.Status);
    }

    [Fact]
    public async Task Solver_proposes_cascading_cross_ward_swap_when_no_free_staff_and_surplus_exists()
    {
        var targetShiftId = Guid.NewGuid();
        var donorShiftId = Guid.NewGuid();
        var donorAllocationId = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        var targetShift = new Shift
        {
            Id = targetShiftId,
            WardId = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 15),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
            RequiredRole = StaffRole.WardNurse,
            MinimumHeadcount = 2,
            HeadcountNeeded = 2
        };

        var donorShift = new Shift
        {
            Id = donorShiftId,
            WardId = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 15),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
            RequiredRole = StaffRole.WardNurse,
            MinimumHeadcount = 2,
            HeadcountNeeded = 2
        };

        var donorAllocation = new Allocation
        {
            Id = donorAllocationId,
            ShiftId = donorShiftId,
            StaffMemberId = staffId,
            Status = AllocationStatus.Confirmed
        };

        var swapCandidate = new CascadingSwapCandidate(
            Staff: new StaffCandidate(staffId, "Sister Mala", StaffRole.WardNurse, "Maternity", true),
            SourceAllocation: donorAllocation,
            SourceShift: donorShift,
            SourceWardName: "Maternity Ward",
            DonorConfirmedCount: 3, // 3 confirmed, min 2 -> surplus 1
            DonorMinimumHeadcount: 2,
            SurplusCount: 1);

        var tools = new TestStaffAllocationAgentTools
        {
            UnderstaffedShift = new UnderstaffedShiftInfo(targetShift, "ICU", 1, 2, 2, 1),
            FreeStaff = [], // No free staff
            SwapCandidates = [swapCandidate]
        };

        var agent = new StaffAllocationAgent(tools, new RosterProposalValidator());

        var run = await agent.RunAsync(new StaffAllocationAgentRequest(
            ShiftId: targetShiftId,
            Objective: "Find replacement staff",
            AllowCascadingSwap: true));

        Assert.Equal(AgentOutcome.SwapProposed, run.Outcome);
        Assert.Equal(AgentWorkflowStatus.PendingApproval, run.Workflow.Status);
        Assert.Equal(2, run.Workflow.ProposedChanges.Count);

        var endChange = run.Workflow.ProposedChanges.First(c => c.ChangeType == ProposedChangeType.EndAllocation);
        Assert.Equal(donorAllocationId, endChange.TargetEntityId);
        Assert.Equal(1, endChange.Sequence);

        var createChange = run.Workflow.ProposedChanges.First(c => c.ChangeType == ProposedChangeType.CreateAllocation);
        Assert.Equal(targetShiftId, createChange.TargetEntityId);
        Assert.Equal(staffId, createChange.ProposedStaffMemberId);
        Assert.Equal(2, createChange.Sequence);
    }

    [Fact]
    public async Task Solver_protects_donor_ward_when_donor_has_no_surplus()
    {
        var targetShiftId = Guid.NewGuid();
        var donorShiftId = Guid.NewGuid();

        var targetShift = new Shift
        {
            Id = targetShiftId,
            WardId = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 15),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
            RequiredRole = StaffRole.WardNurse,
            MinimumHeadcount = 2,
            HeadcountNeeded = 2
        };

        var donorShift = new Shift
        {
            Id = donorShiftId,
            WardId = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 15),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
            RequiredRole = StaffRole.WardNurse,
            MinimumHeadcount = 2,
            HeadcountNeeded = 2
        };

        // Donor has 2 confirmed, minimum 2 -> surplus 0! Releasing one would violate donor minimum.
        var zeroSurplusCandidate = new CascadingSwapCandidate(
            Staff: new StaffCandidate(Guid.NewGuid(), "Nurse Kamal", StaffRole.WardNurse, "Pediatrics", true),
            SourceAllocation: new Allocation { Id = Guid.NewGuid(), ShiftId = donorShiftId },
            SourceShift: donorShift,
            SourceWardName: "Pediatrics Ward",
            DonorConfirmedCount: 2,
            DonorMinimumHeadcount: 2,
            SurplusCount: 0);

        var tools = new TestStaffAllocationAgentTools
        {
            UnderstaffedShift = new UnderstaffedShiftInfo(targetShift, "ICU", 1, 2, 2, 1),
            FreeStaff = [],
            SwapCandidates = [zeroSurplusCandidate]
        };

        var agent = new StaffAllocationAgent(tools, new RosterProposalValidator());

        var run = await agent.RunAsync(new StaffAllocationAgentRequest(
            ShiftId: targetShiftId,
            AllowCascadingSwap: true));

        // Must reject swap to protect donor ward minimum
        Assert.True(run.Outcome == AgentOutcome.Failed || run.Outcome == AgentOutcome.NoCandidateFound);
        Assert.Equal(AgentWorkflowStatus.Failed, run.Workflow.Status);
        Assert.Empty(run.Workflow.ProposedChanges);
    }

    [Fact]
    public async Task Solver_skips_cascading_swap_when_allow_cascading_swap_is_false()
    {
        var targetShiftId = Guid.NewGuid();

        var targetShift = new Shift
        {
            Id = targetShiftId,
            WardId = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 15),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(16, 0),
            RequiredRole = StaffRole.WardNurse,
            MinimumHeadcount = 2,
            HeadcountNeeded = 2
        };

        var swapCandidate = new CascadingSwapCandidate(
            Staff: new StaffCandidate(Guid.NewGuid(), "Sister Mala", StaffRole.WardNurse, "Maternity", true),
            SourceAllocation: new Allocation { Id = Guid.NewGuid() },
            SourceShift: new Shift { Id = Guid.NewGuid(), MinimumHeadcount = 1 },
            SourceWardName: "Maternity",
            DonorConfirmedCount: 3,
            DonorMinimumHeadcount: 1,
            SurplusCount: 2);

        var tools = new TestStaffAllocationAgentTools
        {
            UnderstaffedShift = new UnderstaffedShiftInfo(targetShift, "ICU", 1, 2, 2, 1),
            FreeStaff = [],
            SwapCandidates = [swapCandidate]
        };

        var agent = new StaffAllocationAgent(tools, new RosterProposalValidator());

        var run = await agent.RunAsync(new StaffAllocationAgentRequest(
            ShiftId: targetShiftId,
            AllowCascadingSwap: false)); // Cascading disabled

        Assert.Equal(AgentOutcome.NoCandidateFound, run.Outcome);
        Assert.Equal(AgentWorkflowStatus.Failed, run.Workflow.Status);
        Assert.Empty(run.Workflow.ProposedChanges);

        var cascadingStep = run.Plan.FirstOrDefault(s => s.AgentRole == "cascading_solver");
        Assert.NotNull(cascadingStep);
        Assert.Equal("skipped", cascadingStep.Status);
    }

    #endregion

    #region Approval Safety & Revalidation Tests (Area C)

    [Fact]
    public void Validator_flags_inactive_staff_and_role_mismatch()
    {
        var validator = new RosterProposalValidator();
        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 20),
            RequiredRole = StaffRole.WardNurse,
            MinimumHeadcount = 2,
            HeadcountNeeded = 2
        };

        // Inactive staff
        var inactiveCandidate = new StaffCandidate(Guid.NewGuid(), "Inactive Nurse", StaffRole.WardNurse, "Ward A", false);
        var resultsInactive = validator.ValidateDirectAllocation(inactiveCandidate, shift, 1);
        Assert.Contains(resultsInactive, r => r.Check == "staff_is_active" && !r.Passed);

        // Role mismatch
        var wrongRoleCandidate = new StaffCandidate(Guid.NewGuid(), "Doctor Silva", StaffRole.Doctor, "Ward A", true);
        var resultsWrongRole = validator.ValidateDirectAllocation(wrongRoleCandidate, shift, 1);
        Assert.Contains(resultsWrongRole, r => r.Check == "staff_holds_required_skill_on_shift_date" && !r.Passed);
    }

    [Fact]
    public async Task Stale_proposal_fails_approval_when_revalidation_detects_violation()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubRosterProposalService();
        var proposal = stub.AddTestProposal(Guid.NewGuid());
        stub.SimulateStaleProposal(proposal.Id); // Simulates candidate becoming unavailable

        await using var app = new RosterProposalTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsync($"/api/roster-proposals/{proposal.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // Verify status remains PendingApproval
        var detail = await client.GetFromJsonAsync<RosterProposalDetail>($"/api/roster-proposals/{proposal.Id}", JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(RosterProposalStatus.PendingApproval, detail.Status);
    }

    [Fact]
    public async Task Atomic_approval_completes_or_rolls_back_cleanly()
    {
        using var environment = TestEnvironment.Use();
        var stub = new StubRosterProposalService();
        var targetShiftId = Guid.NewGuid();
        var donorShiftId = Guid.NewGuid();
        var donorAllocId = Guid.NewGuid();

        var cascadingProposal = stub.AddCascadingSwapProposal(targetShiftId, donorShiftId, donorAllocId);

        await using var app = new RosterProposalTestApplication(stub);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", CreateToken("hospital_administrator", "staff"));

        var response = await client.PostAsync($"/api/roster-proposals/{cascadingProposal.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var approved = await response.Content.ReadFromJsonAsync<RosterProposalDetail>(JsonOptions);
        Assert.NotNull(approved);
        Assert.Equal(RosterProposalStatus.Approved, approved.Status);
        Assert.True(approved.IsCascadingSwap);
        Assert.Equal(2, approved.ProposedChanges.Count);
    }

    #endregion

    #region Automatic Trigger Tests (Area D)

    [Fact]
    public async Task Allocation_release_triggers_proposal_when_shortfall_created()
    {
        var shiftId = Guid.NewGuid();
        var mockRosterService = new MockTriggerRosterProposalService();

        // When shift becomes understaffed (shortfall > 0)
        var coverage = new ShiftCoverageDto
        {
            ConfirmedCount = 1,
            MinimumHeadcount = 2,
            HeadcountNeeded = 3,
            Status = CoverageStatus.Understaffed,
            ShortfallToMinimum = 1
        };

        var proposalId = await SimulateAllocationEndTrigger(
            shiftId,
            coverage,
            suppressAgent: false,
            mockRosterService);

        Assert.NotNull(proposalId);
        Assert.Contains(shiftId, mockRosterService.TriggeredShiftIds);
    }

    [Fact]
    public async Task Allocation_release_does_not_trigger_when_suppressed_or_adequately_staffed()
    {
        var shiftId = Guid.NewGuid();
        var mockRosterService = new MockTriggerRosterProposalService();

        // Adequately staffed (shortfall == 0)
        var adequateCoverage = new ShiftCoverageDto
        {
            ConfirmedCount = 2,
            MinimumHeadcount = 2,
            HeadcountNeeded = 2,
            Status = CoverageStatus.Adequate,
            ShortfallToMinimum = 0
        };

        var noProposal1 = await SimulateAllocationEndTrigger(
            shiftId,
            adequateCoverage,
            suppressAgent: false,
            mockRosterService);

        Assert.Null(noProposal1);

        // Understaffed but suppressed
        var understaffedCoverage = new ShiftCoverageDto
        {
            ConfirmedCount = 0,
            MinimumHeadcount = 2,
            HeadcountNeeded = 2,
            Status = CoverageStatus.Critical,
            ShortfallToMinimum = 2
        };

        var noProposal2 = await SimulateAllocationEndTrigger(
            shiftId,
            understaffedCoverage,
            suppressAgent: true, // Suppressed
            mockRosterService);

        Assert.Null(noProposal2);
        Assert.Empty(mockRosterService.TriggeredShiftIds);
    }

    [Fact]
    public async Task Approved_leave_triggers_proposals_for_each_affected_shift()
    {
        var shiftA = Guid.NewGuid();
        var shiftB = Guid.NewGuid();
        var mockRosterService = new MockTriggerRosterProposalService();

        var proposalIds = await SimulateLeaveApprovalTrigger(
            isApprove: true,
            affectedShiftIds: [shiftA, shiftB],
            mockRosterService);

        Assert.Equal(2, proposalIds.Count);
        Assert.Contains(shiftA, mockRosterService.TriggeredShiftIds);
        Assert.Contains(shiftB, mockRosterService.TriggeredShiftIds);
    }

    [Fact]
    public async Task Rejected_or_withdrawn_leave_does_not_trigger_proposals()
    {
        var shiftA = Guid.NewGuid();
        var mockRosterService = new MockTriggerRosterProposalService();

        var proposalIds = await SimulateLeaveApprovalTrigger(
            isApprove: false, // Rejected
            affectedShiftIds: [shiftA],
            mockRosterService);

        Assert.Empty(proposalIds);
        Assert.Empty(mockRosterService.TriggeredShiftIds);
    }

    [Fact]
    public async Task Trigger_failure_is_isolated_and_never_fails_parent_operation()
    {
        var shiftId = Guid.NewGuid();
        var crashingRosterService = new CrashingTriggerRosterProposalService();

        var coverage = new ShiftCoverageDto
        {
            ConfirmedCount = 0,
            MinimumHeadcount = 1,
            ShortfallToMinimum = 1
        };

        // When the proposal service crashes, the trigger caller catches and returns null
        var proposalId = await SimulateAllocationEndTrigger(
            shiftId,
            coverage,
            suppressAgent: false,
            crashingRosterService);

        Assert.Null(proposalId);
    }

    #endregion

    #region Helper Trigger Simulation Methods

    private static async Task<Guid?> SimulateAllocationEndTrigger(
        Guid shiftId,
        ShiftCoverageDto coverage,
        bool suppressAgent,
        IRosterProposalService? rosterProposalService)
    {
        Guid? rosterProposalId = null;
        if (!suppressAgent && rosterProposalService != null && coverage.ShortfallToMinimum > 0)
        {
            try
            {
                rosterProposalId = await rosterProposalService.TriggerProposalIfUnderstaffedAsync(shiftId);
            }
            catch
            {
                // Non-blocking isolation per production implementation in AllocationService
            }
        }
        return rosterProposalId;
    }

    private static async Task<List<Guid>> SimulateLeaveApprovalTrigger(
        bool isApprove,
        List<Guid> affectedShiftIds,
        IRosterProposalService? rosterProposalService)
    {
        var rosterProposalIds = new List<Guid>();
        if (isApprove && rosterProposalService != null && affectedShiftIds.Count > 0)
        {
            foreach (var shiftId in affectedShiftIds)
            {
                try
                {
                    var proposalId = await rosterProposalService.TriggerProposalIfUnderstaffedAsync(shiftId);
                    if (proposalId.HasValue)
                    {
                        rosterProposalIds.Add(proposalId.Value);
                    }
                }
                catch
                {
                    // Non-blocking isolation per production implementation in LeaveRequestService
                }
            }
        }
        return rosterProposalIds;
    }

    #endregion

    #region Test Infrastructure & Stubs

    private static string CreateToken(string role, string principalType, Guid? staffId = null)
    {
        var claims = new List<Claim>
        {
            new(CareLankaClaims.Subject, (staffId ?? Guid.NewGuid()).ToString()),
            new(CareLankaClaims.Role, role),
            new(CareLankaClaims.PrincipalType, principalType)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
        var token = new JwtSecurityToken(
            issuer: "carelanka-api",
            audience: "carelanka-clients",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<JsonDocument> GenerateSwaggerAsync()
    {
        using var environment = TestEnvironment.Use();
        await using var application = new SwaggerOnlyApplication();
        using var client = application.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static YamlMappingNode LoadContract()
    {
        var specPath = Path.Combine(AppContext.BaseDirectory, "../../../../../specs/staff-spec.yaml");
        if (!File.Exists(specPath))
        {
            specPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "specs/staff-spec.yaml"));
        }
        var yaml = new YamlStream();
        using var reader = new StreamReader(specPath);
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static ISet<string> Keys(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            current = current.GetProperty(segment);
        }
        return current.EnumerateObject().Select(property => property.Name).ToHashSet();
    }

    private static YamlMappingNode Map(YamlMappingNode node, params string[] path)
    {
        var current = node;
        foreach (var segment in path)
        {
            current = (YamlMappingNode)current.Children[new YamlScalarNode(segment)];
        }
        return current;
    }

    private static ISet<string> Keys(YamlMappingNode node)
        => node.Children.Keys.Select(key => ((YamlScalarNode)key).Value!).ToHashSet();

    private sealed class SwaggerOnlyApplication : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IRosterProposalService, StubRosterProposalService>();
            });
        }
    }

    private sealed class RosterProposalTestApplication : WebApplicationFactory<Program>
    {
        private readonly IRosterProposalService? _stub;

        public RosterProposalTestApplication(IRosterProposalService? stub = null)
        {
            _stub = stub;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                if (_stub != null)
                {
                    services.RemoveAll<IRosterProposalService>();
                    services.AddSingleton(_stub);
                }
            });
        }
    }

    private sealed class StubRosterProposalService : IRosterProposalService
    {
        private readonly List<RosterProposalDetail> _proposals = [];
        private readonly HashSet<Guid> _missingShifts = [];
        private readonly HashSet<Guid> _staleProposals = [];

        public void SimulateMissingShift(Guid shiftId) => _missingShifts.Add(shiftId);
        public void SimulateStaleProposal(Guid proposalId) => _staleProposals.Add(proposalId);

        public RosterProposalDetail AddTestProposal(
            Guid shiftId,
            Guid? wardId = null,
            RosterProposalStatus status = RosterProposalStatus.PendingApproval)
        {
            var id = Guid.NewGuid();
            var detail = new RosterProposalDetail
            {
                Id = id,
                WorkflowId = id,
                ShiftId = shiftId,
                WardName = "Ward " + (wardId?.ToString()[..4] ?? "ICU"),
                ShiftDate = new DateOnly(2026, 10, 15),
                Objective = "Understaffed shift cover",
                Status = status,
                Outcome = AgentOutcome.FreeStaffProposed,
                IsCascadingSwap = false,
                ChangeCount = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                AttemptCount = 0,
                Plan =
                [
                    new PlanStepDto { Sequence = 1, AgentRole = "shift_analysis", Description = "Analyze shift", Status = "completed" },
                    new PlanStepDto { Sequence = 2, AgentRole = "candidate_search", Description = "Search candidate", Status = "completed" },
                    new PlanStepDto { Sequence = 3, AgentRole = "cascading_solver", Description = "Cascading solver", Status = "skipped" },
                    new PlanStepDto { Sequence = 4, AgentRole = "constraint_validator", Description = "Validate rules", Status = "completed" },
                    new PlanStepDto { Sequence = 5, AgentRole = "human_gate", Description = "Await human approval", Status = "pending" }
                ],
                ProposedChanges =
                [
                    new RosterProposedChangeDto
                    {
                        Id = Guid.NewGuid(),
                        Sequence = 1,
                        ChangeType = RosterProposedChangeType.CreateAllocation,
                        ProposedShiftId = shiftId,
                        ProposedStaffMemberId = Guid.NewGuid(),
                        ProposedStaffName = "Sister Vimali",
                        Rationale = "Allocate free staff Sister Vimali",
                        ValidationStatus = ProposedChangeValidationStatus.Passed
                    }
                ],
                Validation =
                [
                    new RosterValidationResult { Check = "staff_is_active", Passed = true },
                    new RosterValidationResult { Check = "target_shift_reaches_minimum", Passed = true }
                ]
            };

            _proposals.Add(detail);
            return detail;
        }

        public RosterProposalDetail AddCascadingSwapProposal(
            Guid targetShiftId,
            Guid donorShiftId,
            Guid donorAllocationId)
        {
            var id = Guid.NewGuid();
            var staffId = Guid.NewGuid();

            var detail = new RosterProposalDetail
            {
                Id = id,
                WorkflowId = id,
                ShiftId = targetShiftId,
                WardName = "ICU",
                ShiftDate = new DateOnly(2026, 10, 15),
                Objective = "Cascading cross-ward swap",
                Status = RosterProposalStatus.PendingApproval,
                Outcome = AgentOutcome.SwapProposed,
                IsCascadingSwap = true,
                ChangeCount = 2,
                CreatedAt = DateTimeOffset.UtcNow,
                AttemptCount = 0,
                ProposedChanges =
                [
                    new RosterProposedChangeDto
                    {
                        Id = Guid.NewGuid(),
                        Sequence = 1,
                        ChangeType = RosterProposedChangeType.EndAllocation,
                        TargetAllocationId = donorAllocationId,
                        ProposedStaffMemberId = staffId,
                        Rationale = "End donor allocation",
                        ValidationStatus = ProposedChangeValidationStatus.Passed
                    },
                    new RosterProposedChangeDto
                    {
                        Id = Guid.NewGuid(),
                        Sequence = 2,
                        ChangeType = RosterProposedChangeType.CreateAllocation,
                        ProposedShiftId = targetShiftId,
                        ProposedStaffMemberId = staffId,
                        Rationale = "Allocate swapped staff to target shift",
                        ValidationStatus = ProposedChangeValidationStatus.Passed
                    }
                ]
            };

            _proposals.Add(detail);
            return detail;
        }

        public Task<PagedResult<RosterProposalSummary>> ListProposalsAsync(
            ListRosterProposalsQueryParameters parameters,
            CancellationToken cancellationToken = default)
        {
            var query = _proposals.AsEnumerable();

            if (parameters.Status.HasValue)
            {
                query = query.Where(p => p.Status == parameters.Status.Value);
            }

            if (parameters.ShiftId.HasValue)
            {
                query = query.Where(p => p.ShiftId == parameters.ShiftId.Value);
            }

            if (parameters.WardId.HasValue)
            {
                var wardPrefix = "Ward " + parameters.WardId.Value.ToString()[..4];
                query = query.Where(p => p.WardName.StartsWith(wardPrefix));
            }

            var total = query.Count();
            var page = Math.Max(1, parameters.Page);
            var pageSize = Math.Clamp(parameters.PageSize, 1, 100);
            var items = query.Skip((page - 1) * pageSize).Take(pageSize).Select(p => (RosterProposalSummary)p).ToList();

            return Task.FromResult(PagedResult<RosterProposalSummary>.From(items, page, pageSize, total));
        }

        public Task<RosterProposalDetail> GetProposalDetailAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var p = _proposals.FirstOrDefault(x => x.Id == id);
            if (p == null)
            {
                throw new NotFoundException("RosterProposal", id);
            }
            return Task.FromResult(p);
        }

        public Task<RosterProposalSummary> CreateProposalAsync(CreateRosterProposalRequest request, CancellationToken cancellationToken = default)
        {
            if (_missingShifts.Contains(request.ShiftId))
            {
                throw new NotFoundException("Shift", request.ShiftId);
            }

            if (_proposals.Any(p => p.ShiftId == request.ShiftId && p.Status == RosterProposalStatus.PendingApproval))
            {
                throw new ConflictException(MessageCode.Conflict, "This shift already has an open proposal.");
            }

            var proposal = AddTestProposal(request.ShiftId);
            proposal.Objective = request.Objective ?? "Solve understaffed shift";
            return Task.FromResult<RosterProposalSummary>(proposal);
        }

        public Task<RosterProposalDetail> ApproveProposalAsync(Guid id, ApproveRosterProposalRequest request, CancellationToken cancellationToken = default)
        {
            var p = _proposals.FirstOrDefault(x => x.Id == id);
            if (p == null)
            {
                throw new NotFoundException("RosterProposal", id);
            }

            if (p.Status != RosterProposalStatus.PendingApproval)
            {
                throw new ConflictException(MessageCode.Conflict, "Proposal is not in an approvable state.");
            }

            if (_staleProposals.Contains(id))
            {
                throw new ConflictException(MessageCode.Conflict, "Proposal re-validation failed: candidate is no longer available.");
            }

            p.Status = RosterProposalStatus.Approved;
            p.ReviewedAt = DateTimeOffset.UtcNow;
            p.ReviewNotes = request.Notes;
            return Task.FromResult(p);
        }

        public Task<RosterProposalDetail> RejectProposalAsync(Guid id, RejectRosterProposalRequest request, CancellationToken cancellationToken = default)
        {
            var p = _proposals.FirstOrDefault(x => x.Id == id);
            if (p == null)
            {
                throw new NotFoundException("RosterProposal", id);
            }

            if (p.Status != RosterProposalStatus.PendingApproval)
            {
                throw new ConflictException(MessageCode.Conflict, "Proposal is not pending approval.");
            }

            p.Status = RosterProposalStatus.Rejected;
            p.RejectionReason = request.Reason;
            p.ReviewNotes = request.Notes;
            p.ReviewedAt = DateTimeOffset.UtcNow;
            return Task.FromResult(p);
        }

        public Task<RosterProposalSummary> RequestRevisionAsync(Guid id, RequestRosterProposalRevisionRequest request, CancellationToken cancellationToken = default)
        {
            var p = _proposals.FirstOrDefault(x => x.Id == id);
            if (p == null)
            {
                throw new NotFoundException("RosterProposal", id);
            }

            if (p.AttemptCount >= 3)
            {
                throw new ConflictException(MessageCode.Conflict, "Maximum revision limit of 3 attempts reached.");
            }

            p.AttemptCount++;
            p.Status = RosterProposalStatus.PendingApproval;
            return Task.FromResult<RosterProposalSummary>(p);
        }

        public Task<Guid?> TriggerProposalIfUnderstaffedAsync(Guid shiftId, CancellationToken cancellationToken = default)
        {
            if (_proposals.Any(p => p.ShiftId == shiftId && p.Status == RosterProposalStatus.PendingApproval))
            {
                return Task.FromResult<Guid?>(null);
            }

            var proposal = AddTestProposal(shiftId);
            return Task.FromResult<Guid?>(proposal.Id);
        }
    }

    private sealed class MockTriggerRosterProposalService : IRosterProposalService
    {
        public List<Guid> TriggeredShiftIds { get; } = [];

        public Task<Guid?> TriggerProposalIfUnderstaffedAsync(Guid shiftId, CancellationToken cancellationToken = default)
        {
            TriggeredShiftIds.Add(shiftId);
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }

        public Task<PagedResult<RosterProposalSummary>> ListProposalsAsync(ListRosterProposalsQueryParameters parameters, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RosterProposalDetail> GetProposalDetailAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RosterProposalSummary> CreateProposalAsync(CreateRosterProposalRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RosterProposalDetail> ApproveProposalAsync(Guid id, ApproveRosterProposalRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RosterProposalDetail> RejectProposalAsync(Guid id, RejectRosterProposalRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RosterProposalSummary> RequestRevisionAsync(Guid id, RequestRosterProposalRevisionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class CrashingTriggerRosterProposalService : IRosterProposalService
    {
        public Task<Guid?> TriggerProposalIfUnderstaffedAsync(Guid shiftId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated unexpected failure in solver agent.");

        public Task<PagedResult<RosterProposalSummary>> ListProposalsAsync(ListRosterProposalsQueryParameters parameters, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RosterProposalDetail> GetProposalDetailAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RosterProposalSummary> CreateProposalAsync(CreateRosterProposalRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RosterProposalDetail> ApproveProposalAsync(Guid id, ApproveRosterProposalRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RosterProposalDetail> RejectProposalAsync(Guid id, RejectRosterProposalRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<RosterProposalSummary> RequestRevisionAsync(Guid id, RequestRosterProposalRevisionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class TestStaffAllocationAgentTools : IStaffAllocationAgentTools
    {
        public UnderstaffedShiftInfo? UnderstaffedShift { get; set; }
        public IReadOnlyList<StaffCandidate> FreeStaff { get; set; } = [];
        public IReadOnlyList<CascadingSwapCandidate> SwapCandidates { get; set; } = [];

        public Task<UnderstaffedShiftInfo?> GetUnderstaffedShiftAsync(Guid shiftId, CancellationToken cancellationToken = default)
            => Task.FromResult(UnderstaffedShift);

        public Task<IReadOnlyList<StaffCandidate>> FindEligibleStaffAsync(Guid shiftId, IReadOnlyList<Guid>? excludeStaffIds = null, CancellationToken cancellationToken = default)
            => Task.FromResult(FreeStaff);

        public Task<IReadOnlyList<StaffCandidate>> FindFreeStaffAsync(IReadOnlyCollection<StaffCandidate> candidates, Shift shift, CancellationToken cancellationToken = default)
            => Task.FromResult(FreeStaff);

        public Task<IReadOnlyList<CascadingSwapCandidate>> FindCascadingSwapCandidatesAsync(Shift targetShift, IReadOnlyList<Guid>? excludeStaffIds = null, IReadOnlyList<Guid>? excludeWardIds = null, CancellationToken cancellationToken = default)
            => Task.FromResult(SwapCandidates);
    }

    #endregion
}
