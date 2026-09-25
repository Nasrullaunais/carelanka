using CareLanka.Api.Common.Errors;
using CareLanka.Api.Common.Exceptions;
using CareLanka.Api.Data.Enums;
using PatientEntity = CareLanka.Api.Data.Entities.Patient.Patient;

namespace CareLanka.Api.Services.Patient;

internal static class PatientIdentityRules
{
    public static void EnsureMayChange(
        PatientEntity patient, string fullName, string? nic, Gender gender, PrincipalRole role)
    {
        if (patient.Nic is null || role == PrincipalRole.HospitalAdministrator)
        {
            return;
        }

        if (fullName != patient.FullName || nic != patient.Nic || gender != patient.Gender)
        {
            throw new ForbiddenException(MessageCode.PatientIdentityLocked);
        }
    }
}