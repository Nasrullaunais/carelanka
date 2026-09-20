using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<AppointmentStatus>))]
public enum AppointmentStatus
{
    /// <summary>Booked in the app. Nobody at the desk has looked at it yet.</summary>
    Scheduled,

    /// <summary>The desk confirmed it. The patient has not arrived, and the date may be weeks out.</summary>
    Confirmed,

    /// <summary>
    /// Over, either way. The patient was seen and went home - the bill is on this appointment -
    /// or they were admitted, and <c>AdmissionId</c> says which.
    /// </summary>
    Completed,

    Cancelled,

    /// <summary>Was expected, the time has passed, and they never came.</summary>
    NoShow
}
