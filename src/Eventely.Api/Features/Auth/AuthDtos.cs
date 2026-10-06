using System.ComponentModel.DataAnnotations;

namespace Eventely.Api.Features.Auth;

public sealed class RegisterRequest
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; init; } = string.Empty;
}

public sealed class VerifyOtpRequest
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; init; } = string.Empty;

    [Required, RegularExpression("^\\d{6}$", ErrorMessage = "The verification code must contain six digits.")]
    public string Code { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed class GoogleLoginRequest
{
    [Required, StringLength(4096)]
    public string IdToken { get; init; } = string.Empty;
}

public sealed class ResendOtpRequest
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; init; } = string.Empty;
}

public sealed record OtpDispatchResponse(string Message);

public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string Role,
    string? FirstName,
    string? LastName);

public sealed record AuthResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAtUtc,
    bool RequiresProfileCompletion,
    AuthUserResponse User);
