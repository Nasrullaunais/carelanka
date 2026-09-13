using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

/// <summary>
/// What paperwork is outstanding on a patient record, as the closed vocabulary of
/// <see cref="PatientDetailField"/>.
/// </summary>
/// <remarks>
/// Static and dependency-free, for the same reason <see cref="BedLabels"/> is: there is no
/// state and no rule here beyond the list of fields, and two callers had otherwise each
/// written it out. <c>Admission.MissingFields</c> is where the ward clerk reads it and
/// <c>MyProfile.MissingFields</c> is where the patient reads it — <b>the two must never
/// disagree</b>, or the app asks for a field the desk is not chasing.
/// </remarks>
public static class PatientDetailChecklist
{
    /// <summary>
    /// The fields still blank on this record. Field names rather than a count, because "two
    /// things missing" does not tell a ward clerk what to chase, and does not tell a patient
    /// what to type.
    /// </summary>
    public static List<string> MissingFor(PatientEntity patient)
    {
        var missing = new List<string>();

        void Require(PatientDetailField field, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add(EnumWire.ToWire(field));
            }
        }

        Require(PatientDetailField.Nic, patient.Nic);
        Require(PatientDetailField.FullName, patient.FullName);
        Require(PatientDetailField.Phone, patient.Phone);
        Require(PatientDetailField.Address, patient.Address);
        Require(PatientDetailField.EmergencyContactName, patient.EmergencyContactName);
        Require(PatientDetailField.EmergencyContactPhone, patient.EmergencyContactPhone);

        if (patient.DateOfBirth is null)
        {
            missing.Add(EnumWire.ToWire(PatientDetailField.DateOfBirth));
        }

        return missing;
    }
}
