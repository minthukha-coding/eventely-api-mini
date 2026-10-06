using Eventely.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventely.Api.Features.Profile;

[ApiController]
[Authorize]
[Route("api/profile")]
[Produces("application/json")]
public sealed class ProfileController(IProfileService profileService) : ControllerBase
{
    /// <summary>Return the authenticated user's profile.</summary>
    [HttpGet("me")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> GetMe(CancellationToken cancellationToken)
    {
        var profile = await profileService.GetMeAsync(CurrentUser.GetId(User), cancellationToken);
        return Ok(profile);
    }

    /// <summary>Update profile fields and optionally upload or remove the profile picture.</summary>
    /// <remarks>Send as multipart/form-data. The complete profile requires first name, last name, address, gender, and a profile photo.</remarks>
    [HttpPut]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<ProfileResponse>> Update(
        [FromForm] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await profileService.UpdateAsync(CurrentUser.GetId(User), request, cancellationToken);
        return Ok(profile);
    }
}
