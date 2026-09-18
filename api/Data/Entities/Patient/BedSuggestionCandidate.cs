namespace CareLanka.Api.Data.Entities.Patient;

/// <summary>
/// One selectable bed. The best pick and every alternative are the same shape on purpose:
/// alternatives used to be a bed id and a sentence saying why it lost, which a nurse could read
/// and could not act on.
/// </summary>
public class BedSuggestionCandidate : AuditedEntity
{
    public Guid BedSuggestionId { get; set; }

    public BedSuggestion BedSuggestion { get; set; } = null!;

    /// <summary>
    /// No foreign key, the same as <c>BedAssignment.BedId</c>: beds are Equipment's register and
    /// <c>IBedRegistryService</c> is the one file in this component that knows the table exists.
    /// </summary>
    public Guid BedId { get; set; }

    public Guid WardId { get; set; }

    /// <summary>1 for the agent's top pick, then 2, 3, … in ranked order.</summary>
    public int Rank { get; set; }

    public bool IsDowngrade { get; set; }

    public bool RequiresDutyManager { get; set; }

    /// <summary>The hard rules this bed was checked against and passed, named as H0-H6 read.</summary>
    public List<string> RulesSatisfied { get; set; } = [];

    /// <summary>
    /// A short sentence for the person reading the screen. The model's raw reasoning is never
    /// stored - §6 forbids it.
    /// </summary>
    public string? Rationale { get; set; }
}
