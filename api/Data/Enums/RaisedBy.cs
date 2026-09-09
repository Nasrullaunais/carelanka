namespace CareLanka.Api.Data.Enums;

// Whether a warning or an action came from the monitoring agent or from a person.
// The agent-performance report counts on being able to tell them apart.
public enum RaisedBy
{
    Agent,
    User
}
