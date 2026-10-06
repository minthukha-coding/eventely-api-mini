using Eventely.Api.Domain.Constants;
using Eventely.Api.Domain.Enums;

namespace Eventely.Api.Domain.Entities;

public sealed class ApplicationUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public string? PasswordHash { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Address { get; set; }
    public Gender? Gender { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public string? ProfilePicturePublicId { get; set; }
    public string Role { get; set; } = UserRoles.NormalUser;
    public bool IsEmailConfirmed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Event> CreatedEvents { get; set; } = new List<Event>();
    public ICollection<EventParticipant> EventParticipations { get; set; } = new List<EventParticipant>();

    public bool IsProfileComplete =>
        !string.IsNullOrWhiteSpace(FirstName) &&
        !string.IsNullOrWhiteSpace(LastName) &&
        !string.IsNullOrWhiteSpace(Address) &&
        Gender.HasValue &&
        !string.IsNullOrWhiteSpace(ProfilePictureUrl);
}
