namespace CareLanka.Api.DTOs.Emergency;

public sealed class AmbulanceDetail : Ambulance
{
    public DispatchSummary? ActiveDispatch { get; set; }
    public bool IsDivertible { get; set; }
    public int RunsToday { get; set; }
}
