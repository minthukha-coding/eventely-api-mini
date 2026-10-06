namespace Eventely.Api.Domain.Entities;

public sealed class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Location { get; set; }
    public DateTimeOffset EventDateTime { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? CoverImagePublicId { get; set; }
    public Guid CreatorId { get; set; }
    public ApplicationUser Creator { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<EventImage> Images { get; set; } = new List<EventImage>();
    public ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();
}
