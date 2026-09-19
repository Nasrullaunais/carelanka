namespace CareLanka.Api.Services.Equipment;

public sealed class EquipmentOptions
{
    public const string SectionName = "Equipment";

    public const string ConfirmationCodeHeader = "X-Confirmation-Code";

    public string ConfirmationCode { get; set; } = string.Empty;

    /// <summary>How often the warning sweep runs by itself. 0 turns the timer off; the Run check
    /// button still works.</summary>
    public int WarningSweepIntervalMinutes { get; set; } = 60;

    /// <summary>A batch with stock left that expires within this many days raises a warning.</summary>
    public int ExpiryWarningDays { get; set; } = 30;
}
