namespace CareLanka.Api.Data.Enums;

/// <summary>
/// Group-owned vocabulary, published by <c>specs/common-spec.yaml</c>.
/// </summary>
public enum AgentWorkflowStatus
{
    Running,
    AwaitingApproval,
    Approved,
    Rejected,
    RevisionRequested,
    Completed,
    Failed
}
