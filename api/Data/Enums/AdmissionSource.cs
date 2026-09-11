using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;


// Appears in a query string on GET /api/admissions, so it needs the converter as well
// as the JSON one: the model binder matches the C# member name, and awaiting_bed does
// not match AwaitingBed.
[TypeConverter(typeof(SnakeCaseEnumTypeConverter<AdmissionSource>))]
public enum AdmissionSource
{
    Emergency,
    WalkIn,
    PreRegistered
}
