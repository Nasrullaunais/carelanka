namespace CareLanka.Api.Data.Enums;

public enum EmergencyCallOutcome
{
    Transported,
    TreatedAtScene,
    RefusedTransport,
    PatientNotFound,
    DeceasedAtScene,
    FalseAlarm,
    DuplicateCall,
    CallerCancelled,
    NoLongerNeeded
}
