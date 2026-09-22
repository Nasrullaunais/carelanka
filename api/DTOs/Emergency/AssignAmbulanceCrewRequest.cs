using System.Text.Json.Serialization;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class AssignAmbulanceCrewRequest
{
    [JsonRequired]
    public Guid StaffMemberId { get; set; }
}
