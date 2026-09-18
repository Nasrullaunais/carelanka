using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Enums;

namespace CareLanka.Api.DTOs.Patient;

/// <summary>Null when nothing is in the way.</summary>
public class BedSuggestionBlocker
{
    [Required]
    public BedSuggestionBlockerCode Code { get; set; }

    [Required]
    public string Message { get; set; } = null!;
}
