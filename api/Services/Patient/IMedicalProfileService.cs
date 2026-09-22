using CareLanka.Api.DTOs.Patient;
using MedicalProfileResponse = CareLanka.Api.DTOs.Patient.PatientMedicalProfile;

namespace CareLanka.Api.Services.Patient;

public interface IMedicalProfileService
{
    Task<MedicalProfileResponse> GetAsync(
        Guid patientId, CancellationToken cancellationToken = default);

    Task<MedicalProfileResponse> ReplaceAsync(
        Guid patientId,
        UpdateMedicalProfileRequest request,
        CancellationToken cancellationToken = default);
}
