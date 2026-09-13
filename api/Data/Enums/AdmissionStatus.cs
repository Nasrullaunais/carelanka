using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;

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
