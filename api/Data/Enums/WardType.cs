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
    Isolation,

    // Added 2026-09-11 with the real ward board. Each is its own type rather than a
    // differently-named general ward because the bed-day rate is read off the ward type -
    // a surgical bed and an ordinary one are not the same price, and with one type they
    // could never be told apart on a bill.
    Surgical,
    Emergency,
    MentalHealth
}
