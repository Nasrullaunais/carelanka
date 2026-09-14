using CareLanka.Api.Common.Persistence;
using CareLanka.Api.Data.Enums;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

public static class PatientDetailChecklist
{
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
