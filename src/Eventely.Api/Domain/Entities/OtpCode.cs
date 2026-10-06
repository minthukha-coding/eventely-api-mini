namespace Eventely.Api.Domain.Entities;

public sealed class OtpCode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    // Contains an ASP.NET PasswordHasher value, never the six digit code sent by email.
    public required string Code { get; set; }
    public DateTimeOffset ExpirationTime { get; set; }
    public bool IsUsed { get; set; }
    public int FailedAttempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
