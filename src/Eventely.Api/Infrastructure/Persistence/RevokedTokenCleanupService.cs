using Microsoft.EntityFrameworkCore;

namespace Eventely.Api.Infrastructure.Persistence;

public sealed class RevokedTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<RevokedTokenCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var expiredCount = await db.RevokedTokens
                    .Where(token => token.ExpiresAt <= timeProvider.GetUtcNow())
                    .ExecuteDeleteAsync(stoppingToken);

                if (expiredCount > 0)
                {
                    logger.LogInformation("Removed {Count} expired JWT revocation records.", expiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not clean up expired JWT revocation records.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
