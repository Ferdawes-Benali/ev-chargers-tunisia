using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EvChargers.Application.Common;
using EvChargers.Application.Interfaces;

namespace EvChargers.Infrastructure.Companion;

/// <summary>
/// Keeps every verified station's nearby places in the database so companion requests never wait on Overpass.
/// A minute after startup, then every 30 min, it refreshes up to 20 due stations (see <see cref="PlacesWarmup"/>): missing,
/// stale (24 h, or 6 h when nothing was found) or failed and past their backoff delay (30 min, doubling, capped at 24 h),
/// least recently attempted first. Each run is one cheap query when nothing is due.
/// Stations queued through <see cref="ChannelPlacesRefreshQueue"/> go first. One station at a time, with a pause
/// between them, to stay polite to the shared Overpass servers.
/// </summary>
public class CompanionWarmupService : BackgroundService
{
    private readonly ChannelPlacesRefreshQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<CompanionWarmupService> _logger;

    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(60);
    /// <summary>As short as the first retry delay, so a first failure is retried at the next run.</summary>
    public TimeSpan SweepInterval { get; init; } = PlacesWarmup.RetryFailedAfter;
    public TimeSpan PauseBetweenStations { get; init; } = TimeSpan.FromSeconds(5);

    public CompanionWarmupService(ChannelPlacesRefreshQueue queue, IServiceScopeFactory scopeFactory, TimeProvider clock,
        ILogger<CompanionWarmupService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextSweep = _clock.GetUtcNow() + StartupDelay;
        var batch = new Queue<Guid>();
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Requested stations (a visit found no places, an admin verified one) before the sweep
                if (!_queue.TryDequeue(out var stationId) && !batch.TryDequeue(out stationId))
                {
                    var untilSweep = nextSweep - _clock.GetUtcNow();
                    if (untilSweep > TimeSpan.Zero)
                    {
                        await WaitForRequestAsync(untilSweep, stoppingToken);
                        continue;
                    }

                    nextSweep = _clock.GetUtcNow() + SweepInterval;
                    foreach (var id in await SelectStaleAsync(stoppingToken)) batch.Enqueue(id);
                    continue;
                }

                await RefreshAsync(stationId, stoppingToken);
                await Task.Delay(PauseBetweenStations, _clock, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }

    private async Task WaitForRequestAsync(TimeSpan timeout, CancellationToken stoppingToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout, _clock);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeoutCts.Token);
        try
        {
            await _queue.WaitToReadAsync(wait.Token);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // time for the next sweep
        }
    }

    private async Task<List<Guid>> SelectStaleAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<IPlacesCacheRepository>();
            var selected = PlacesWarmup.Select(await cache.GetWarmupCandidatesAsync(ct), _clock.GetUtcNow().UtcDateTime);
            if (selected.Count > 0)
                _logger.LogInformation("Refreshing nearby places for {Count} stations", selected.Count);
            return selected;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Could not choose stations for the places warmup; trying again at the next run");
            return [];
        }
    }

    private async Task RefreshAsync(Guid stationId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IPlacesCacheRefresher>().RefreshAsync(stationId, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // e.g. the database is briefly unreachable; the next sweep picks the station up again
            _logger.LogError(ex, "Places refresh failed for station {StationId}", stationId);
        }
    }
}
