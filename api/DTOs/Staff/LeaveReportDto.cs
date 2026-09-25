namespace CareLanka.Api.DTOs.Staff;

public class LeaveReport
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public string GroupBy { get; set; } = string.Empty;

    public IReadOnlyList<LeaveReportRow> Rows { get; set; } = Array.Empty<LeaveReportRow>();
}

public class LeaveReportRow
{
    public string Key { get; set; } = string.Empty;

    public double ApprovedDays { get; set; }

    public double PendingDays { get; set; }

    public int RejectedCount { get; set; }

    public double SickDays { get; set; }
}

public sealed class LeaveReportDto : LeaveReport
{
}
