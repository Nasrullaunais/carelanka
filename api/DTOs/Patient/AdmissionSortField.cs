using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>What GET /api/admissions sorts on. Four fields, because those are the four the spec publishes.</summary>
[TypeConverter(typeof(SnakeCaseEnumTypeConverter<AdmissionSortField>))]
public enum AdmissionSortField
{
    CreatedAt,
    ExpectedArrival,
    AdmittedAt,
    Urgency
}
