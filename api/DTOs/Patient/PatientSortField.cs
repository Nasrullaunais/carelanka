using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.DTOs.Patient;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<PatientSortField>))]
public enum PatientSortField
{
    FullName,
    CreatedAt
}
