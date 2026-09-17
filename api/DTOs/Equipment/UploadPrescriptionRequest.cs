namespace CareLanka.Api.DTOs.Equipment;

// A multipart form rather than JSON, because it carries the photographed prescription itself.
public sealed class UploadPrescriptionRequest
{
    public IFormFile? File { get; set; }

    public string? Note { get; set; }
}
