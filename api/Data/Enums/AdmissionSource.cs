using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<AdmissionSource>))]
public enum AdmissionSource
{
    Emergency,
    WalkIn,
    PreRegistered
}
