using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public class EmergencyReportRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed class ResponseTimeReportRequest : EmergencyReportRequest
{
    public CallPriority? Priority { get; set; }
}
