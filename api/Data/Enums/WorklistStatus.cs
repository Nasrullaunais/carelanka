using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.Data.Enums;

// What the ward board says about one person, in the words a nurse uses.
//
// **Derived, never stored.** It is a reading of two other stored statuses - AppointmentStatus
// for a booking and AdmissionStatus for a visit - and it exists because neither of those on
// its own answers "what is happening with this patient". Nothing transitions between these
// values; the transition rules live on AdmissionStatus, which is the authoritative one.
//
// A component-specific vocabulary gets a component-specific name, so this is WorklistStatus
// rather than a third claim on "Status". See CLAUDE.md, one app one API surface.
[TypeConverter(typeof(SnakeCaseEnumTypeConverter<WorklistStatus>))]
public enum WorklistStatus
{
    /// <summary>
    /// Booked in and not here yet. The only status a booking can have on this list - a booking
    /// that has been checked in is represented by its admission instead, not twice.
    /// </summary>
    NotArrived,

    /// <summary>
    /// Here, needs a bed, has not got one. Covers a pending agent proposal too: until a bed is
    /// actually held, the board's answer is the same and so is what a nurse does about it.
    /// </summary>
    AwaitingBed,

    /// <summary>
    /// A bed is held for them but nobody has marked them as being in it. **Time-critical:** the
    /// hold expires after thirty minutes and the bed goes back to the pool on its own.
    /// </summary>
    BedReady,

    /// <summary>
    /// In the hospital and being treated - in a bed if the visit needed one, and simply present
    /// if it did not. An outpatient here for a scan reads as admitted, because they are.
    /// </summary>
    Admitted,

    /// <summary>Done and gone home. The scan happened, or the stay ended.</summary>
    Completed,

    /// <summary>Called off. Nobody is coming and nobody is here.</summary>
    Cancelled
}
