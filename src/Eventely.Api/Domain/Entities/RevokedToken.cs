namespace Eventely.Api.Domain.Entities;

public sealed class RevokedToken
{
    public required string JwtId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset RevokedAt { get; set; }
}
