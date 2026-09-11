namespace CareLanka.Api.Data.Enums;

// Unknown exists because an unidentified arrival has no recorded gender, and hard rule H3
// (gender-policy ward filter) still has to do something deterministic with that row.
public enum Gender
{
    Male,
    Female,
    Other,
    Unknown
}
