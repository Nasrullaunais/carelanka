namespace CareLanka.Api.DTOs.Equipment;

// A multipart form rather than JSON, since a doctor writing one directly for a patient they
// looked up may attach a file (a photo or PDF) instead of typing it. Exactly one of Body or
// File is ever given.
public sealed class CreatePrescriptionRequest
{
    public Guid PatientId { get; set; }

    public string? Body { get; set; }

    public IFormFile? File { get; set; }
}
