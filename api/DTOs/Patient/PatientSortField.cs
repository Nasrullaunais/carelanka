using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>What GET /api/patients sorts on. Two fields, because those are the two the spec publishes.</summary>
[TypeConverter(typeof(SnakeCaseEnumTypeConverter<PatientSortField>))]
public enum PatientSortField
{
    FullName,
    CreatedAt
}
