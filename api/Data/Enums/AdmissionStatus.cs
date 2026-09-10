namespace CareLanka.Api.Data.Enums;

// Stored and authoritative, never derived. An illegal move is a 409, and you can only reject
// one by comparing against a stored previous value.
public enum AdmissionStatus
{
    AwaitingBed,
    AwaitingApproval,
    BedReserved,
    Admitted,
    ReadyForDischarge,
    Discharged,
    Cancelled
}
