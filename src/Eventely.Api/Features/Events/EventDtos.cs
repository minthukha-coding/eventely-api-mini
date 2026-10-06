using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Eventely.Api.Features.Events;

public sealed class CreateEventRequest
{
    [Required, StringLength(160, MinimumLength = 3)]
    public string Title { get; init; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 1)]
    public string Description { get; init; } = string.Empty;

    [Required, StringLength(300, MinimumLength = 2)]
    public string Location { get; init; } = string.Empty;

    [Required]
    public DateTimeOffset? EventDateTime { get; init; }

    [Required]
    public IFormFile? CoverImage { get; init; }

    public List<IFormFile> DetailImages { get; init; } = [];
}

public sealed class UpdateEventRequest
{
    [Required, StringLength(160, MinimumLength = 3)]
    public string Title { get; init; } = string.Empty;

    [Required, StringLength(4000, MinimumLength = 1)]
    public string Description { get; init; } = string.Empty;

    [Required, StringLength(300, MinimumLength = 2)]
    public string Location { get; init; } = string.Empty;

    [Required]
    public DateTimeOffset? EventDateTime { get; init; }

    public IFormFile? CoverImage { get; init; }
    public bool ReplaceDetailImages { get; init; }
    public List<IFormFile> DetailImages { get; init; } = [];
}

public sealed record EventCreatorResponse(
    Guid Id,
    string? FirstName,
    string? LastName,
    string? ProfilePictureUrl);

public sealed class EventResponse
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Location { get; init; }
    public DateTimeOffset EventDateTime { get; init; }
    public string? CoverImageUrl { get; init; }
    public required EventCreatorResponse Creator { get; init; }
    public IReadOnlyList<string> DetailImageUrls { get; init; } = Array.Empty<string>();
    public int ParticipantCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
