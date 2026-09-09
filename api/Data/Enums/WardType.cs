namespace CareLanka.Api.Data.Enums;

// Hdu, not HighDependency: the member name is what becomes the wire value, and
// patient-spec.yaml publishes "hdu". Same reason Icu is not ICU.
public enum WardType
{
    Icu,
    Hdu,
    General,
    Maternity,
    Pediatric,
    Isolation
}
