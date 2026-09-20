namespace CareLanka.Api.DTOs.Patient;

public class ClaimByPatientCodeRequest
{
    public string PatientCode { get; set; } = string.Empty;

    public string Nic { get; set; } = string.Empty;
}
