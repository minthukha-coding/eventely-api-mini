using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Eventely.Api.Common;
using Eventely.Api.Domain.Constants;
using Eventely.Api.Domain.Entities;
using Eventely.Api.Infrastructure.Configuration;
using Eventely.Api.Infrastructure.Email;
using Eventely.Api.Infrastructure.Persistence;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Eventely.Api.Features.Auth;

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IEmailSender emailSender,
    IOptions<JwtOptions> jwtOptions,
    IOptions<GoogleOptions> googleOptions,
    IOptions<OtpOptions> otpOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    private const string VerificationSentMessage = "If the account can receive a verification code, one has been sent.";

    public async Task<OtpDispatchResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            throw ApiException.Conflict("An account with this email already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new ApplicationUser
        {
            Email = email,
            Role = UserRoles.NormalUser,
            IsEmailConfirmed = false,
            CreatedAt = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.Users.Add(user);
            await AddAndSendOtpAsync(user, email, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new OtpDispatchResponse(VerificationSentMessage);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            if (await db.Users.AsNoTracking().AnyAsync(existing => existing.Email == email, cancellationToken))
            {
                throw ApiException.Conflict("An account with this email already exists.");
            }

            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<OtpDispatchResponse> ResendVerificationCodeAsync(string emailInput, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(emailInput);
        var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);
        if (user is null || user.IsEmailConfirmed)
        {
            return new OtpDispatchResponse(VerificationSentMessage);
        }

        var now = timeProvider.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await AddAndSendOtpAsync(user, email, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new OtpDispatchResponse(VerificationSentMessage);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);
        if (user is null || user.IsEmailConfirmed)
        {
            throw ApiException.BadRequest("The verification code is invalid or expired.");
        }

        var now = timeProvider.GetUtcNow();
        var otp = await db.OtpCodes
            .Where(candidate => candidate.Email == email && !candidate.IsUsed)
            .OrderByDescending(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null || otp.ExpirationTime <= now)
        {
            throw ApiException.BadRequest("The verification code is invalid or expired.");
        }

        if (passwordHasher.VerifyHashedPassword(user, otp.Code, request.Code) == PasswordVerificationResult.Failed)
        {
            otp.FailedAttempts++;
            if (otp.FailedAttempts >= 5)
            {
                otp.IsUsed = true;
            }

            await db.SaveChangesAsync(cancellationToken);
            throw ApiException.BadRequest("The verification code is invalid or expired.");
        }

        otp.IsUsed = true;
        user.IsEmailConfirmed = true;
        await db.SaveChangesAsync(cancellationToken);
        return CreateAuthResponse(user, now);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);
        if (user?.PasswordHash is null ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            throw ApiException.Unauthorized("Invalid email or password.");
        }

        if (!user.IsEmailConfirmed)
        {
            throw new ApiException(StatusCodes.Status403Forbidden, "Verify your email before logging in.");
        }

        return CreateAuthResponse(user, timeProvider.GetUtcNow());
    }

    public async Task<AuthResponse> LoginWithGoogleAsync(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var clientId = googleOptions.Value.ClientId;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw ApiException.ServiceUnavailable("Google sign-in is not configured.");
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                request.IdToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
        }
        catch (InvalidJwtException)
        {
            throw ApiException.Unauthorized("The Google ID token is invalid or expired.");
        }

        if (!payload.EmailVerified || string.IsNullOrWhiteSpace(payload.Email))
        {
            throw ApiException.Unauthorized("Google did not provide a verified email address.");
        }

        var email = NormalizeEmail(payload.Email);
        var user = await db.Users.SingleOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (user is null)
        {
            user = new ApplicationUser
            {
                Email = email,
                FirstName = payload.GivenName,
                LastName = payload.FamilyName,
                Role = UserRoles.NormalUser,
                IsEmailConfirmed = true,
                CreatedAt = now
            };
            db.Users.Add(user);
        }
        else
        {
            // Google's verified address is sufficient to confirm or link this account.
            user.IsEmailConfirmed = true;
            user.FirstName ??= payload.GivenName;
            user.LastName ??= payload.FamilyName;
        }

        await db.SaveChangesAsync(cancellationToken);
        return CreateAuthResponse(user, now);
    }

    public async Task LogoutAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var jwtId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var userIdValue = principal.FindFirst("userId")?.Value;
        var expiresValue = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (string.IsNullOrWhiteSpace(jwtId) || !Guid.TryParse(userIdValue, out var userId) ||
            !long.TryParse(expiresValue, out var expiresUnix))
        {
            throw ApiException.Unauthorized();
        }

        if (await db.RevokedTokens.AnyAsync(token => token.JwtId == jwtId, cancellationToken))
        {
            return;
        }

        db.RevokedTokens.Add(new RevokedToken
        {
            JwtId = jwtId,
            UserId = userId,
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresUnix),
            RevokedAt = timeProvider.GetUtcNow()
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Concurrent logout requests are idempotent; the unique JWT id is the arbiter.
            db.Entry(db.ChangeTracker.Entries<RevokedToken>().Single().Entity).State = EntityState.Detached;
            if (!await db.RevokedTokens.AnyAsync(token => token.JwtId == jwtId, cancellationToken))
            {
                throw;
            }
        }
    }

    private async Task AddAndSendOtpAsync(
        ApplicationUser user,
        string email,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var unusedCodes = await db.OtpCodes
            .Where(code => code.Email == email && !code.IsUsed)
            .ToListAsync(cancellationToken);
        foreach (var oldCode in unusedCodes)
        {
            oldCode.IsUsed = true;
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var expiresAt = now.AddMinutes(Math.Clamp(otpOptions.Value.ExpirationMinutes, 1, 30));
        db.OtpCodes.Add(new OtpCode
        {
            Email = email,
            Code = passwordHasher.HashPassword(user, code),
            ExpirationTime = expiresAt,
            IsUsed = false,
            CreatedAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        await emailSender.SendVerificationCodeAsync(email, code, expiresAt, cancellationToken);
        logger.LogInformation("An email verification code was sent.");
    }

    private AuthResponse CreateAuthResponse(ApplicationUser user, DateTimeOffset now)
    {
        var settings = jwtOptions.Value;
        var expiresAt = now.AddMinutes(Math.Clamp(settings.AccessTokenMinutes, 1, 60));
        var jwtId = Guid.NewGuid().ToString("N");
        var claims = new List<Claim>
        {
            new("userId", user.Id.ToString()),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("email", user.Email),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("role", user.Role),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);
        var encodedToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthResponse(
            encodedToken,
            "Bearer",
            expiresAt,
            !user.IsProfileComplete,
            new AuthUserResponse(user.Id, user.Email, user.Role, user.FirstName, user.LastName));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
