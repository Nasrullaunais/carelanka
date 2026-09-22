using CareLanka.Api.Data;
using CareLanka.Api.Data.Entities.Common;
using CareLanka.Api.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareLanka.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AgentWorkflowTests
{
    private readonly ApiApplication _application;

    public AgentWorkflowTests(ApiApplication application) => _application = application;

    [Fact]
    public async Task A_workflow_saves_its_plan_and_proposed_changes_and_reads_them_back()
    {
        var workflow = NewWorkflow();
        workflow.Plan = """[{"step":"list_eligible_ambulances"}]""";
        workflow.ProposedChanges.Add(NewChange(1));

        await SaveAsync(workflow);

        using var scope = _application.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .AgentWorkflows.Include(x => x.ProposedChanges).AsNoTracking().SingleAsync(x => x.Id == workflow.Id);
        Assert.Equal(AgentType.DispatchRouting, saved.AgentType);
        Assert.Equal(AgentWorkflowStatus.Pending, saved.Status);
        Assert.Contains("list_eligible_ambulances", saved.Plan);
        Assert.Equal("[]", saved.CompletedSteps);
        var change = Assert.Single(saved.ProposedChanges);
        Assert.Equal(ProposedChangeType.CreateDispatch, change.ChangeType);
        Assert.Equal(ProposedChangeValidationStatus.Pending, change.ValidationStatus);
    }

    [Fact]
    public async Task Two_changes_in_one_workflow_cannot_share_a_sequence_number()
    {
        var workflow = NewWorkflow();
        workflow.ProposedChanges.Add(NewChange(1));
        workflow.ProposedChanges.Add(NewChange(1));

        await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(workflow));
    }

    [Fact]
    public async Task Workflows_in_one_plan_can_be_found_by_their_shared_correlation_id()
    {
        var parent = NewWorkflow();
        await SaveAsync(parent);
        var child = NewWorkflow();
        child.CorrelationId = parent.CorrelationId;
        child.ParentWorkflowId = parent.Id;
        await SaveAsync(child);

        using var scope = _application.Services.CreateScope();
        var ids = await scope.ServiceProvider.GetRequiredService<CareLankaDbContext>()
            .AgentWorkflows.Where(x => x.CorrelationId == parent.CorrelationId).Select(x => x.Id).ToListAsync();
        Assert.Equal(2, ids.Count);
    }

    private async Task SaveAsync(AgentWorkflow workflow)
    {
        using var scope = _application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CareLankaDbContext>();
        db.AgentWorkflows.Add(workflow);
        await db.SaveChangesAsync();
    }

    private static AgentWorkflow NewWorkflow() => new()
    {
        Id = Guid.NewGuid(), AgentType = AgentType.DispatchRouting, EntityType = "EmergencyCall",
        EntityId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(),
        Objective = "Recommend the best eligible ambulance and explain why.", Status = AgentWorkflowStatus.Pending
    };

    private static AgentProposedChange NewChange(int sequence) => new()
    {
        Id = Guid.NewGuid(), Sequence = sequence, ChangeType = ProposedChangeType.CreateDispatch,
        ValidationStatus = ProposedChangeValidationStatus.Pending
    };
}
