using System.Security.Claims;

namespace Eventely.Api.Common;

public static class CurrentUser
{
    public static Guid GetId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst("userId")?.Value;
        return Guid.TryParse(value, out var userId)
            ? userId
            : throw ApiException.Unauthorized();
    }
}
