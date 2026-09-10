using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;


// Stored and authoritative, never derived. An illegal move is a 409, and you can only reject
// one by comparing against a stored previous value.
// Appears in a query string on GET /api/admissions, so it needs the converter as well
// as the JSON one: the model binder matches the C# member name, and awaiting_bed does
// not match AwaitingBed.
[TypeConverter(typeof(SnakeCaseEnumTypeConverter<AdmissionStatus>))]
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
