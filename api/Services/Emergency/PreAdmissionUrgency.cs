using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.Services.Emergency;

public static class PreAdmissionUrgency
{
    public static AdmissionUrgency From(CallPriority priority) => priority switch
    {
        CallPriority.Critical => AdmissionUrgency.Emergency,
        CallPriority.High => AdmissionUrgency.Urgent,
        CallPriority.Medium or CallPriority.Low => AdmissionUrgency.Routine,
        _ => throw new ArgumentOutOfRangeException(nameof(priority))
    };
}
