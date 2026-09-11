using System.ComponentModel;
using CareLanka.Api.Common.Persistence;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>
/// What <c>GET /api/bed-availability</c> filters on: the four states a bed can be in, plus
/// <c>all</c>, which is the default.
/// </summary>
/// <remarks>
/// A separate name from <see cref="BedAvailability"/> rather than one enum with an extra
/// member. <c>all</c> is a question, not an answer — no bed is ever in it — and a response
/// field that could carry it would have every client handle a value the server cannot send.
/// The same reason equipment-spec.yaml and this one keep <c>EquipmentBed</c> and
/// <c>AdmissionBed</c> apart: two shapes, two names.
///
/// It arrives in a query string, so it needs the type converter as well as the JSON one. The
/// model binder matches C# member names, and <c>out_of_service</c> does not match
/// <c>OutOfService</c>.
/// </remarks>
[TypeConverter(typeof(SnakeCaseEnumTypeConverter<BedAvailabilityFilter>))]
public enum BedAvailabilityFilter
{
    Free,
    Reserved,
    Occupied,
    OutOfService,
    All
}
