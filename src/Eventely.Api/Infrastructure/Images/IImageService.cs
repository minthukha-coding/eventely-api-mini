using Microsoft.AspNetCore.Http;

namespace Eventely.Api.Infrastructure.Images;

public sealed record ImageAsset(string Url, string PublicId);

public interface IImageService
{
    Task<ImageAsset> UploadAsync(IFormFile file, string folder, CancellationToken cancellationToken);
    Task DeleteAsync(string publicId, CancellationToken cancellationToken);
}
