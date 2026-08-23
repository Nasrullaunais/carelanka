using System.ComponentModel.DataAnnotations;
using CareLanka.Api.Data.Entities.Common;

namespace CareLanka.Api.DTOs.Common;

/// <summary>
/// Auth request and response shapes. Bodies go over the wire in snake_case
/// (access_token, staff_member) — that conversion is configured once in Program.cs,
/// so property names here stay ordinary C#.
/// </summary>
public record LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, MaxLength(128)]
    public required string Password { get; init; }
}

public record RefreshRequest
{
    [Required]
    public required string RefreshToken { get; init; }
}

public record AuthTokens
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required string TokenType { get; init; } = "Bearer";

    /// <summary>Lifetime of the access token, in seconds.</summary>
    public required int ExpiresIn { get; init; }

    public required AuthenticatedStaff StaffMember { get; init; }
}

/// <summary>The signed-in user, as both frontends need to render a header and gate a menu.</summary>
public record AuthenticatedStaff
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required string FullName { get; init; }
    public required StaffRole Role { get; init; }
    public string? Department { get; init; }
}
