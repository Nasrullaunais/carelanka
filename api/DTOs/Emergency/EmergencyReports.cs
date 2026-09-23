using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class ResponseTimeReport
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public IReadOnlyList<ResponseTimeReportRow> Rows { get; set; } = [];
    public ResponseTimeReportTotals Totals { get; set; } = new();
}

public sealed class ResponseTimeReportRow
{
    public CallPriority Priority { get; set; }
    public int CallCount { get; set; }
    public double MedianMinutesToDispatch { get; set; }
    public double MedianMinutesToArrival { get; set; }
    public double SlowestMinutesToArrival { get; set; }
}

public sealed class ResponseTimeReportTotals
{
    public int CallCount { get; set; }
    public double MedianMinutesToDispatch { get; set; }
    public double MedianMinutesToArrival { get; set; }
}

public sealed class FleetUtilisationReport
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public IReadOnlyList<FleetUtilisationReportRow> Rows { get; set; } = [];
}

public sealed class FleetUtilisationReportRow
{
    public Guid AmbulanceId { get; set; }
    public string RegistrationNumber { get; set; } = string.Empty;
    public int RunCount { get; set; }
    public double HoursCommitted { get; set; }
    public double IdleShare { get; set; }
    public double OutOfServiceHours { get; set; }
}

public sealed class EmergencyAgentPerformanceReport
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int ProposalsRaised { get; set; }
    public int Confirmed { get; set; }
    public double ConfirmedWithoutChangeRate { get; set; }
    public int DiversionsProposed { get; set; }
    public int DiversionsApproved { get; set; }
    public int DiversionsRejected { get; set; }
    public int NoAmbulanceAvailableCount { get; set; }
    public double ValidationFailureRate { get; set; }
    public double MedianSecondsProposalToConfirm { get; set; }
    public double MedianMinutesCallToDispatch { get; set; }
    public IReadOnlyDictionary<string, int> RejectionReasons { get; set; } = new Dictionary<string, int>();
}
