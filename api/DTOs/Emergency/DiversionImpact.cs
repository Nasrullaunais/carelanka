using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class DiversionImpact
{
    public Guid SourceDispatchId { get; set; }
    public Guid SourceCallId { get; set; }
    public CallPriority SourceCallPriority { get; set; }
    public string? SourceCallAddressLabel { get; set; }
    public DispatchStatus SourceDispatchStatus { get; set; }
    public int SourceCallWaitingMinutesSoFar { get; set; }
    public int? SourceCallAdditionalWaitMinutes { get; set; }
    public Guid? ReplacementAmbulanceId { get; set; }
    public string? ReplacementAmbulanceRegistration { get; set; }
    public int? MinutesSavedForThisCall { get; set; }
}
