using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;

[TypeConverter(typeof(SnakeCaseEnumTypeConverter<AdmissionCategory>))]
public enum AdmissionCategory
{
    Icu,
    Hdu,
    Inpatient,
    DayCase,
    Outpatient
}
