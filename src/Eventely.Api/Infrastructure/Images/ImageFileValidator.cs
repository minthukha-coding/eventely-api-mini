using Eventely.Api.Common;
using Microsoft.AspNetCore.Http;

namespace Eventely.Api.Infrastructure.Images;

public static class ImageFileValidator
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;

    public static async Task ValidateAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaxFileSizeBytes)
        {
            throw ApiException.BadRequest("Each image must be between 1 byte and 5 MB.");
        }

        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(), cancellationToken);
        var isJpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var isWebp = read >= 12 && header[0] == (byte)'R' && header[1] == (byte)'I' &&
                    header[2] == (byte)'F' && header[3] == (byte)'F' && header[8] == (byte)'W' &&
                    header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';

        if (!isJpeg && !isPng && !isWebp)
        {
            throw ApiException.BadRequest("Only valid JPEG, PNG, and WebP images are accepted.");
        }
    }
}
