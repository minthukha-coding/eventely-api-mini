using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Eventely.Api.Common;
using Eventely.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Eventely.Api.Infrastructure.Images;

public sealed class CloudinaryImageService : IImageService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryImageService> _logger;

    public CloudinaryImageService(IOptions<CloudinaryOptions> options, ILogger<CloudinaryImageService> logger)
    {
        _logger = logger;
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.CloudName) ||
            string.IsNullOrWhiteSpace(settings.ApiKey) ||
            string.IsNullOrWhiteSpace(settings.ApiSecret))
        {
            throw ApiException.ServiceUnavailable("Image storage is not configured.");
        }

        _cloudinary = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret));
        _cloudinary.Api.Secure = true;
    }

    public async Task<ImageAsset> UploadAsync(IFormFile file, string folder, CancellationToken cancellationToken)
    {
        await ImageFileValidator.ValidateAsync(file, cancellationToken);
        await using var stream = file.OpenReadStream();
        var upload = new ImageUploadParams
        {
            File = new FileDescription(Path.GetFileName(file.FileName), stream),
            Folder = folder
        };

        ImageUploadResult result;
        try
        {
            result = await _cloudinary.UploadAsync(upload);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cloudinary failed to upload an image to {Folder}.", folder);
            throw ApiException.BadGateway("Image storage could not upload the image.");
        }
        if (result.Error is not null || result.SecureUrl is null || string.IsNullOrWhiteSpace(result.PublicId))
        {
            throw ApiException.BadGateway("Image storage could not upload the image.");
        }

        return new ImageAsset(result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task DeleteAsync(string publicId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        DeletionResult result;
        try
        {
            result = await _cloudinary.DestroyAsync(new DeletionParams(publicId));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cloudinary failed to delete image {PublicId}.", publicId);
            throw ApiException.BadGateway("Image storage could not delete the image.");
        }
        if (result.Error is not null)
        {
            throw ApiException.BadGateway("Image storage could not delete the image.");
        }
    }
}
