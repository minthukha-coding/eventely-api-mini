namespace Eventely.Api.Features.Profile;

public interface IProfileService
{
    Task<ProfileResponse> GetMeAsync(Guid userId, CancellationToken cancellationToken);
    Task<ProfileResponse> UpdateAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken);
}
