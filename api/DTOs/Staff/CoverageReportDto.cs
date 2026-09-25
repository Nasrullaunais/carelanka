namespace CareLanka.Api.DTOs.Staff;

public class CoverageReport
{
    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public IReadOnlyList<CoverageReportRow> Rows { get; set; } = Array.Empty<CoverageReportRow>();

    public CoverageReportTotals Totals { get; set; } = new();
}

public class CoverageReportRow
{
    public Guid WardId { get; set; }

    public string WardName { get; set; } = string.Empty;

    public DateOnly Date { get; set; }

    public int ShiftsTotal { get; set; }

    public int ShiftsUnderstaffed { get; set; }

    public double HoursBelowMinimum { get; set; }

    public double FillRate { get; set; }
}

public class CoverageReportTotals
{
    public int ShiftsTotal { get; set; }

    public int ShiftsUnderstaffed { get; set; }

    public double HoursBelowMinimum { get; set; }

    public double FillRate { get; set; }
}

public sealed class CoverageReportDto : CoverageReport
{
}
