namespace CareLanka.Api.Data.Enums;

// A property of the ward, not a rule the bed agent bends under pressure. ICU and pediatric
// wards are Mixed because real intensive care units are open bays.
public enum GenderPolicy
{
    Male,
    Female,
    Mixed
}
