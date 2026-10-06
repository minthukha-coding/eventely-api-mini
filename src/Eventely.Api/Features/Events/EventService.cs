using Eventely.Api.Common;
using Eventely.Api.Domain.Entities;
using Eventely.Api.Infrastructure.Images;
using Eventely.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Eventely.Api.Features.Events;

public sealed class EventService(
    AppDbContext db,
    IImageService imageService,
    TimeProvider timeProvider,
    ILogger<EventService> logger) : IEventService
{
    private const int MaxDetailImages = 10;

    public async Task<PagedResponse<EventResponse>> GetManyAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 50)
        {
            throw ApiException.BadRequest("Page must be at least 1 and pageSize must be between 1 and 50.");
        }

        var query = db.Events.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await Project(query)
            .OrderByDescending(@event => @event.CreatedAt)
            .ThenBy(@event => @event.EventDateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<EventResponse>(items, page, pageSize, totalCount);
    }

    public async Task<EventResponse> GetByIdAsync(Guid eventId, CancellationToken cancellationToken)
    {
        return await Project(db.Events.AsNoTracking())
            .SingleOrDefaultAsync(@event => @event.Id == eventId, cancellationToken)
            ?? throw ApiException.NotFound("The event was not found.");
    }

    public async Task<EventResponse> CreateAsync(
        Guid creatorId,
        CreateEventRequest request,
        CancellationToken cancellationToken)
    {
        ValidateEventFields(request.Title, request.Description, request.Location);
        if (request.CoverImage is null)
        {
            throw ApiException.BadRequest("A cover image is required.");
        }

        ValidateDetailImageCount(request.DetailImages);
        var uploadedAssets = new List<ImageAsset>();
        Event createdEvent;

        try
        {
            var cover = await imageService.UploadAsync(request.CoverImage, "eventely/events/covers", cancellationToken);
            uploadedAssets.Add(cover);
            var detailAssets = await UploadDetailImagesAsync(request.DetailImages, uploadedAssets, cancellationToken);

            createdEvent = new Event
            {
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Location = request.Location.Trim(),
                EventDateTime = request.EventDateTime!.Value.ToUniversalTime(),
                CoverImageUrl = cover.Url,
                CoverImagePublicId = cover.PublicId,
                CreatorId = creatorId,
                CreatedAt = timeProvider.GetUtcNow(),
                Images = detailAssets.Select(asset => new EventImage
                {
                    ImageUrl = asset.Url,
                    PublicId = asset.PublicId
                }).ToList()
            };

            db.Events.Add(createdEvent);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DeleteUploadedAssetsAsync(uploadedAssets, CancellationToken.None);
            throw;
        }

        return await GetByIdAsync(createdEvent.Id, cancellationToken);
    }

    public async Task<EventResponse> UpdateAsync(
        Guid userId,
        Guid eventId,
        UpdateEventRequest request,
        CancellationToken cancellationToken)
    {
        var @event = await db.Events
            .Include(candidate => candidate.Images)
            .SingleOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken)
            ?? throw ApiException.NotFound("The event was not found.");
        EnsureCreator(@event, userId);
        ValidateEventFields(request.Title, request.Description, request.Location);

        if (!request.ReplaceDetailImages && request.DetailImages.Count > 0)
        {
            throw ApiException.BadRequest("Set ReplaceDetailImages to true when sending detail images.");
        }

        if (request.ReplaceDetailImages)
        {
            ValidateDetailImageCount(request.DetailImages);
        }

        var uploadedAssets = new List<ImageAsset>();
        var oldAssetsToDelete = new List<string>();
        var oldCoverPublicId = @event.CoverImagePublicId;
        var replacingCover = request.CoverImage is not null;
        var replacingDetails = request.ReplaceDetailImages;

        try
        {
            ImageAsset? newCover = null;
            if (request.CoverImage is not null)
            {
                newCover = await imageService.UploadAsync(request.CoverImage, "eventely/events/covers", cancellationToken);
                uploadedAssets.Add(newCover);
            }

            var newDetailAssets = new List<ImageAsset>();
            if (replacingDetails)
            {
                newDetailAssets = await UploadDetailImagesAsync(request.DetailImages, uploadedAssets, cancellationToken);
            }

            @event.Title = request.Title.Trim();
            @event.Description = request.Description.Trim();
            @event.Location = request.Location.Trim();
            @event.EventDateTime = request.EventDateTime!.Value.ToUniversalTime();

            if (newCover is not null)
            {
                @event.CoverImageUrl = newCover.Url;
                @event.CoverImagePublicId = newCover.PublicId;
            }

            if (replacingDetails)
            {
                oldAssetsToDelete.AddRange(@event.Images.Select(image => image.PublicId));
                db.EventImages.RemoveRange(@event.Images);
                foreach (var imageAsset in newDetailAssets)
                {
                    @event.Images.Add(new EventImage
                    {
                        EventId = @event.Id,
                        ImageUrl = imageAsset.Url,
                        PublicId = imageAsset.PublicId
                    });
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DeleteUploadedAssetsAsync(uploadedAssets, CancellationToken.None);
            throw;
        }

        if (replacingCover && !string.IsNullOrWhiteSpace(oldCoverPublicId))
        {
            oldAssetsToDelete.Add(oldCoverPublicId);
        }

        await DeleteOldAssetsBestEffortAsync(oldAssetsToDelete, cancellationToken);
        return await GetByIdAsync(@event.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        var @event = await db.Events
            .Include(candidate => candidate.Images)
            .SingleOrDefaultAsync(candidate => candidate.Id == eventId, cancellationToken)
            ?? throw ApiException.NotFound("The event was not found.");
        EnsureCreator(@event, userId);

        var publicIds = @event.Images.Select(image => image.PublicId).ToList();
        if (!string.IsNullOrWhiteSpace(@event.CoverImagePublicId))
        {
            publicIds.Add(@event.CoverImagePublicId);
        }

        db.Events.Remove(@event);
        await db.SaveChangesAsync(cancellationToken);
        await DeleteOldAssetsBestEffortAsync(publicIds, cancellationToken);
    }

    public async Task JoinAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        if (!await db.Events.AnyAsync(@event => @event.Id == eventId, cancellationToken))
        {
            throw ApiException.NotFound("The event was not found.");
        }

        if (await db.EventParticipants.AnyAsync(
                participant => participant.EventId == eventId && participant.UserId == userId,
                cancellationToken))
        {
            return;
        }

        var participant = new EventParticipant
        {
            EventId = eventId,
            UserId = userId,
            JoinedAt = timeProvider.GetUtcNow()
        };
        db.EventParticipants.Add(participant);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(participant).State = EntityState.Detached;
            if (!await db.EventParticipants.AnyAsync(
                    item => item.EventId == eventId && item.UserId == userId,
                    cancellationToken))
            {
                throw;
            }
        }
    }

    public async Task LeaveAsync(Guid userId, Guid eventId, CancellationToken cancellationToken)
    {
        if (!await db.Events.AnyAsync(@event => @event.Id == eventId, cancellationToken))
        {
            throw ApiException.NotFound("The event was not found.");
        }

        var participant = await db.EventParticipants.SingleOrDefaultAsync(
            item => item.EventId == eventId && item.UserId == userId,
            cancellationToken);
        if (participant is null)
        {
            return;
        }

        db.EventParticipants.Remove(participant);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<EventResponse> Project(IQueryable<Event> query) => query.Select(@event => new EventResponse
    {
        Id = @event.Id,
        Title = @event.Title,
        Description = @event.Description,
        Location = @event.Location,
        EventDateTime = @event.EventDateTime,
        CoverImageUrl = @event.CoverImageUrl,
        CreatedAt = @event.CreatedAt,
        Creator = new EventCreatorResponse(
            @event.Creator.Id,
            @event.Creator.FirstName,
            @event.Creator.LastName,
            @event.Creator.ProfilePictureUrl),
        DetailImageUrls = @event.Images.OrderBy(image => image.Id).Select(image => image.ImageUrl).ToList(),
        ParticipantCount = @event.Participants.Count
    });

    private async Task<List<ImageAsset>> UploadDetailImagesAsync(
        IReadOnlyCollection<IFormFile> files,
        ICollection<ImageAsset> uploadedAssets,
        CancellationToken cancellationToken)
    {
        var assets = new List<ImageAsset>(files.Count);
        foreach (var file in files)
        {
            var asset = await imageService.UploadAsync(file, "eventely/events/details", cancellationToken);
            uploadedAssets.Add(asset);
            assets.Add(asset);
        }

        return assets;
    }

    private static void ValidateDetailImageCount(IReadOnlyCollection<IFormFile> files)
    {
        if (files.Count > MaxDetailImages)
        {
            throw ApiException.BadRequest($"An event can have at most {MaxDetailImages} detail images.");
        }
    }

    private static void ValidateEventFields(string title, string description, string location)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(location))
        {
            throw ApiException.BadRequest("Title, description, and location must contain non-whitespace characters.");
        }
    }

    private static void EnsureCreator(Event @event, Guid userId)
    {
        if (@event.CreatorId != userId)
        {
            throw ApiException.Forbidden("Only the creator of this event can change or delete it.");
        }
    }

    private async Task DeleteUploadedAssetsAsync(IEnumerable<ImageAsset> assets, CancellationToken cancellationToken)
    {
        foreach (var asset in assets)
        {
            try
            {
                await imageService.DeleteAsync(asset.PublicId, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not clean up newly uploaded image {PublicId}.", asset.PublicId);
            }
        }
    }

    private async Task DeleteOldAssetsBestEffortAsync(IEnumerable<string> publicIds, CancellationToken cancellationToken)
    {
        foreach (var publicId in publicIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
        {
            try
            {
                await imageService.DeleteAsync(publicId, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not delete replaced or removed image {PublicId}.", publicId);
            }
        }
    }
}
