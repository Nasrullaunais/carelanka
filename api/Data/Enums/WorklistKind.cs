using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<WorklistKind>))]
public enum WorklistKind
{
    Booking,

    Visit
}
