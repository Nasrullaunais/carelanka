namespace CareLanka.Api.Data.Enums;

// The closed vocabulary of Admission.MissingFields. A closed set rather than free text, so
// "what paperwork is still outstanding" can be counted and filtered instead of parsed.
// The members match CompleteDetailsRequest's keys — those are the fields that can be filled
// in later.
public enum PatientDetailField
{
    Nic,
    FullName,
    DateOfBirth,
    Phone,
    Address,
    EmergencyContactName,
    EmergencyContactPhone
}
