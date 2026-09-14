using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.DTOs.Patient;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<AdmissionSortField>))]
public enum AdmissionSortField
{
    CreatedAt,
    ExpectedArrival,
    AdmittedAt,
    Urgency
}
