namespace CareLanka.Api.Data.Enums;

// Whether the agent proposed this or a human picked it. Routes the approval and the audit.
public enum AssignedBy
{
    Agent,
    User
}
