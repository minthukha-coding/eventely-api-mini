using System.ComponentModel.DataAnnotations;
using Eventely.Api.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Eventely.Api.Features.Profile;

public sealed class UpdateProfileRequest : IValidatableObject
{
    [Required, StringLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, StringLength(500)]
    public string Address { get; init; } = string.Empty;

    [Required]
    public Gender? Gender { get; init; }

    public IFormFile? ProfilePicture { get; init; }
    public bool RemoveProfilePicture { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RemoveProfilePicture && ProfilePicture is not null)
        {
            yield return new ValidationResult(
                "Upload a new profile picture or remove the existing one, but not both.",
                [nameof(ProfilePicture), nameof(RemoveProfilePicture)]);
        }
    }
}

public sealed record ProfileResponse(
    Guid Id,
    string Email,
    bool IsEmailConfirmed,
    string Role,
    string? FirstName,
    string? LastName,
    string? Address,
    Gender? Gender,
    string? ProfilePictureUrl,
    bool IsProfileComplete,
    DateTimeOffset CreatedAt);
