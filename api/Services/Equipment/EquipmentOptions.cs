namespace CareLanka.Api.Services.Equipment;

public sealed class EquipmentOptions
{
    public const string SectionName = "Equipment";

    public const string ConfirmationCodeHeader = "X-Confirmation-Code";

    public string ConfirmationCode { get; set; } = string.Empty;
}
