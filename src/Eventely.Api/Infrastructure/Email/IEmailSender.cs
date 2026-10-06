namespace Eventely.Api.Infrastructure.Email;

public interface IEmailSender
{
    Task SendVerificationCodeAsync(string email, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken);
}
