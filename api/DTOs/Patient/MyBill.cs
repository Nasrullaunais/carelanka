using System.ComponentModel.DataAnnotations;

namespace CareLanka.Api.DTOs.Patient;

public class MyBill
{
    /// <summary>
    /// Exactly one of <see cref="AdmissionId"/> and <see cref="AppointmentId"/> is set,
    /// mirroring the staff <c>Bill</c> entity.
    /// </summary>
    public Guid? AdmissionId { get; set; }

    public Guid? AppointmentId { get; set; }

    [Required]
    public string BillNumber { get; set; } = string.Empty;

    [Required]
    public string Currency { get; set; } = "LKR";

    [Required]
    public IReadOnlyList<MyBillLine> Lines { get; set; } = Array.Empty<MyBillLine>();

    [Required]
    public decimal Total { get; set; }

    /// <summary>
    /// False while the admission is still open: bed nights keep accruing, so the
    /// total will grow. The app must not show it as the amount due.
    /// </summary>
    [Required]
    public bool IsFinal { get; set; }

    [Required]
    public bool Settled { get; set; }

    public DateTimeOffset? SettledAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }
}
