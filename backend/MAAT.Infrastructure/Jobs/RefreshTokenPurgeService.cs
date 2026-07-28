using MAAT.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MAAT.Infrastructure.Jobs;

// docs/specs/auth-securite-rgpd.md, section 3 : les jetons révoqués ne sont jamais
// supprimés à la révocation (nécessaire à la détection de réutilisation), mais doivent
// être purgés 30 jours après leur expiration ou leur révocation.
public class RefreshTokenPurgeService(
    IServiceScopeFactory scopeFactory,
    ILogger<RefreshTokenPurgeService> logger) : BackgroundService
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            await PurgeAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var refreshTokenRepository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();

            var cutoff = DateTimeOffset.UtcNow - RetentionPeriod;
            var purgedCount = await refreshTokenRepository.PurgeExpiredOrRevokedBeforeAsync(cutoff, ct);

            if (purgedCount > 0)
            {
                logger.LogInformation(
                    "Purge des refresh tokens : {PurgedCount} jeton(s) supprimé(s) (expirés ou révoqués depuis plus de 30 jours).",
                    purgedCount);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Échec de la purge des refresh tokens.");
        }
    }
}
