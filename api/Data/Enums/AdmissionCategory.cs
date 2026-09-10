namespace CareLanka.Api.Data.Enums;

// Ordered most to least acute, so the bed agent's downgrade ladder is an ordinal step.
// Hdu, not HighDependency: the member name becomes the wire value and the spec publishes "hdu".
public enum AdmissionCategory
{
    Icu,
    Hdu,
    Inpatient,
    DayCase,
    Outpatient
}
