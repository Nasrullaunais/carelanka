using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

// What a patient may see of their own result. A separate shape, not a filtered staff
// LabReport - same reasoning as MyBill. It carries no uploaded_by_staff_id and no
// patient_id, because a patient reading their own list already knows both.
public class MyLabReport
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string TestName { get; set; } = string.Empty;

    public string? Summary { get; set; }

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    public int ByteSize { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }
}
