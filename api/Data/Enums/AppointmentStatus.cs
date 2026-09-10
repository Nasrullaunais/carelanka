using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;

// checked_in is terminal for the appointment. From there the record of what happens next is
// the Admission, not this row.
//
// Appears in a query string on GET /api/appointments, so it needs the type converter as well
// as the JSON one: the model binder matches the C# member name, and checked_in does not
// match CheckedIn.
[TypeConverter(typeof(SnakeCaseEnumTypeConverter<AppointmentStatus>))]
public enum AppointmentStatus
{
    Scheduled,
    CheckedIn,
    Completed,
    Cancelled,
    NoShow
}
