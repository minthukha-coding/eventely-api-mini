using Eventely.Api.Common;
using Eventely.Api.Domain.Entities;
using Eventely.Api.Infrastructure.Images;
using Eventely.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Eventely.Api.Features.Profile;

public sealed class ProfileService(
    AppDbContext db,
    IImageService imageService,
    ILogger<ProfileService> logger) : IProfileService
{
    public async Task<ProfileResponse> GetMeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            ?? throw ApiException.NotFound("The user profile was not found.");
        return Map(user);
    }

    public async Task<ProfileResponse> UpdateAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            ?? throw ApiException.NotFound("The user profile was not found.");

        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Address))
        {
            throw ApiException.BadRequest("First name, last name, and address must contain non-whitespace characters.");
        }

        ImageAsset? newImage = null;
        var oldPublicId = user.ProfilePicturePublicId;
        if (request.ProfilePicture is not null)
        {
            newImage = await imageService.UploadAsync(request.ProfilePicture, "eventely/profiles", cancellationToken);
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Address = request.Address.Trim();
        user.Gender = request.Gender;

        if (newImage is not null)
        {
            user.ProfilePictureUrl = newImage.Url;
            user.ProfilePicturePublicId = newImage.PublicId;
        }
        else if (request.RemoveProfilePicture)
        {
            user.ProfilePictureUrl = null;
            user.ProfilePicturePublicId = null;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (newImage is not null)
            {
                await TryDeleteImageAsync(newImage.PublicId, CancellationToken.None);
            }

            throw;
        }

        if ((newImage is not null || request.RemoveProfilePicture) && !string.IsNullOrWhiteSpace(oldPublicId))
        {
            await TryDeleteImageAsync(oldPublicId, cancellationToken);
        }

        return Map(user);
    }

    private async Task TryDeleteImageAsync(string publicId, CancellationToken cancellationToken)
    {
        try
        {
            await imageService.DeleteAsync(publicId, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not delete replaced profile image {PublicId}.", publicId);
        }
    }

    private static ProfileResponse Map(ApplicationUser user) => new(
        user.Id,
        user.Email,
        user.IsEmailConfirmed,
        user.Role,
        user.FirstName,
        user.LastName,
        user.Address,
        user.Gender,
        user.ProfilePictureUrl,
        user.IsProfileComplete,
        user.CreatedAt);
}
