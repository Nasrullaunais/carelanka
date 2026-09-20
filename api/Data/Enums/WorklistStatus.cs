using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<WorklistStatus>))]
public enum WorklistStatus
{
    AwaitingBed,

    BedReady,

    Admitted,

    Completed,

    Cancelled
}
