using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;

// Which table a worklist row came out of, and therefore what can be done to it. A booking is
// checked in; a visit gets a bed, or is completed. A screen that cannot tell them apart offers
// the wrong button.
[TypeConverter(typeof(SnakeCaseEnumTypeConverter<WorklistKind>))]
public enum WorklistKind
{
    /// <summary>An Appointment nobody has checked in yet. The patient is not here.</summary>
    Booking,

    /// <summary>An Admission. The record of a visit that has started.</summary>
    Visit
}
