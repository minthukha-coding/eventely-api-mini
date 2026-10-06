using System.Security.Claims;

namespace Eventely.Api.Features.Auth;

public interface IAuthService
{
    Task<OtpDispatchResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<OtpDispatchResponse> ResendVerificationCodeAsync(string email, CancellationToken cancellationToken);
    Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginWithGoogleAsync(GoogleLoginRequest request, CancellationToken cancellationToken);
    Task LogoutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
