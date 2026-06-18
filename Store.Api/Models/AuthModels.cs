using System.ComponentModel.DataAnnotations;

namespace Store.Api.Models;

public sealed class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public string? FullName { get; set; }
}

public sealed class LoginRequest
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class AuthResponse
{
    public required string AccessToken { get; set; }

    public required DateTimeOffset ExpiresAt { get; set; }

    public required long UserId { get; set; }

    public required string Email { get; set; }

    public string? FullName { get; set; }
}

public sealed class AccountProfile
{
    public long Id { get; set; }

    public string? Email { get; set; }

    public string? UserName { get; set; }

    public string? FullName { get; set; }

    public string? PhoneNumber { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = [];
}

public sealed class UpdateProfileRequest
{
    public string? FullName { get; set; }

    public string? PhoneNumber { get; set; }
}
