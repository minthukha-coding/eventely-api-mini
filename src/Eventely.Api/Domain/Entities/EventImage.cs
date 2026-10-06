namespace Eventely.Api.Domain.Entities;

public sealed class EventImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public required string ImageUrl { get; set; }
    public required string PublicId { get; set; }
}
