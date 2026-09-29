using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Emergency;

public sealed class EndAtSceneRequest
{
    public SceneOutcome? Outcome { get; set; }
    public string? Notes { get; set; }
}
