namespace CareLanka.Api.DTOs.Staff;

public class StaffAgentPerformanceReport
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public int ProposalsRaised { get; set; }

    public int ProposalsAutoTriggered { get; set; }

    public double ValidationFailureRate { get; set; }

    public int Approved { get; set; }

    public int Rejected { get; set; }

    public int RevisionRequested { get; set; }

    public int FailedSafely { get; set; }

    public int CascadingSwaps { get; set; }

    public double MedianMinutesGapToFill { get; set; }

    public IReadOnlyDictionary<string, int> RejectionReasons { get; set; } = new Dictionary<string, int>();
}

public sealed class StaffAgentPerformanceReportDto : StaffAgentPerformanceReport
{
}
