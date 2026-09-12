using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Body of POST /api/me/pre-register — the patient filling in their own details from the app.
/// </summary>
/// <remarks>
/// <b>Details, not a visit.</b> This is the form that joins a phone login to a medical record:
/// it matches on NIC, links the record it finds, and creates one only when there is nothing to
/// find. Booking a date is <c>POST /me/appointments</c>, and being admitted is a staff action
/// at the desk.
///
/// There is deliberately no <c>expected_arrival</c> on here, and no admission is created. An
/// <c>Admission</c> carries <c>category_set_by_staff_member_id</c>, which is the recorded proof
/// that a human clinician chose the care level — a patient tapping a form on their phone has
/// no staff id and no business choosing their own. See patient-spec.yaml, where this endpoint's
/// contract was corrected on 2026-09-12.
/// </remarks>
public class PreRegisterRequest
{
    /// <summary>
    /// The national identity card number. Required here, unlike at the desk, because it is the
    /// only thing that can match this signup to a record the hospital already holds.
    /// </summary>
    /// <remarks>
    /// <b>NIC alone is not proof of identity.</b> Nothing here verifies that the person typing
    /// it owns it. What limits the damage is that linking to an existing record never
    /// overwrites what is already on it — see <c>MeService.PreRegisterAsync</c>. A production
    /// system would put an OTP or a desk check in front of this.
    /// </remarks>
    [Required]
    [MaxLength(20)]
    public string Nic { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = null!;

    /// <summary>
    /// Nullable so an omitted key is a 400 rather than a silent <c>male</c> — the same reason
    /// the enums on every other request body in this component are nullable.
    /// </summary>
    [Required]
    [EnumDataType(typeof(Gender))]
    public Gender? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(200)]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20)]
    public string? EmergencyContactPhone { get; set; }
}
