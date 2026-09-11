namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// Whether a bed can be used right now. Computed from Equipment's condition and our own
/// assignment rows — never stored, because two sources of truth for "is bed 12 free" drift.
/// </summary>
/// <remarks>
/// A hold past its <c>reserved_until</c> reports <c>Free</c>, with nobody having done
/// anything. See <see cref="Services.Patient.BedHold"/>.
///
/// One value per bed, so the three non-free reasons have an order. A live claim wins over
/// <c>OutOfService</c>: a withdrawn bed with a patient still in it would otherwise report
/// <c>out_of_service</c> while also naming the admission occupying it, which is one row
/// saying two things. Equipment refuses to withdraw an occupied bed, so it should not arise
/// at all — this is which way it reads if it ever does.
/// </remarks>
public enum BedAvailability
{
    Free,
    Reserved,
    Occupied,
    OutOfService
}
