using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;


// Ordered most to least acute, so the bed agent's downgrade ladder is an ordinal step.
// Hdu, not HighDependency: the member name becomes the wire value and the spec publishes "hdu".
// Appears in a query string on GET /api/admissions, so it needs the converter as well
// as the JSON one: the model binder matches the C# member name, and awaiting_bed does
// not match AwaitingBed.
[TypeConverter(typeof(SnakeCaseEnumTypeConverter<AdmissionCategory>))]
public enum AdmissionCategory
{
    Icu,
    Hdu,
    Inpatient,
    DayCase,
    Outpatient
}
