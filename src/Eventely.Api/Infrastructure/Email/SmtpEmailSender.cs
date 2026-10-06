using System.Net;
using System.Net.Mail;
using Eventely.Api.Common;
using Eventely.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Eventely.Api.Infrastructure.Email;

public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendVerificationCodeAsync(
        string email,
        string code,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host) ||
            string.IsNullOrWhiteSpace(settings.FromEmail) ||
            string.IsNullOrWhiteSpace(settings.Username) ||
            string.IsNullOrWhiteSpace(settings.Password))
        {
            throw ApiException.ServiceUnavailable("Email delivery is not configured.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, settings.FromName),
            Subject = "Your Eventely verification code",
            Body = $"Your verification code is {WebUtility.HtmlEncode(code)}. It expires at {expiresAt:O}.",
            IsBodyHtml = false
        };
        message.To.Add(email);

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            Credentials = new NetworkCredential(settings.Username, settings.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await client.SendMailAsync(message);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or WebException)
        {
            logger.LogError(exception, "SMTP could not deliver a verification message.");
            throw ApiException.ServiceUnavailable("Email delivery is temporarily unavailable.");
        }
    }
}
