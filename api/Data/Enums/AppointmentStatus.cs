using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<AppointmentStatus>))]
public enum AppointmentStatus
{
    Scheduled,
    CheckedIn,
    Completed,
    Cancelled,
    NoShow
}
