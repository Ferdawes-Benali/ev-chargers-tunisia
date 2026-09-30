using Microsoft.Extensions.Logging;
using EvChargers.Application.Common;
using EvChargers.Application.Interfaces;

namespace EvChargers.Application.Services;

/// <summary>The only place that calls the places provider: run by the background warmup, never during a request.</summary>
public class PlacesCacheRefresher : IPlacesCacheRefresher
{
    public const string ProviderUnavailableError = "Places provider unavailable: every server failed or timed out";

    private readonly IStationRepository _stations;
    private readonly IPlacesProvider _places;
    private readonly IPlacesCacheRepository _cache;
    private readonly TimeProvider _clock;
    private readonly ILogger<PlacesCacheRefresher> _logger;

    public PlacesCacheRefresher(IStationRepository stations, IPlacesProvider places, IPlacesCacheRepository cache,
        TimeProvider clock, ILogger<PlacesCacheRefresher> logger)
    {
        _stations = stations;
        _places = places;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    public async Task<bool> RefreshAsync(Guid stationId, CancellationToken ct)
    {
        var station = await _stations.GetByIdAsync(stationId, ct);
        if (station is null) return false;

        // Same radius the companion uses for this station's charge time
        var (_, chargeMinutes) = CompanionService.ChargeOf(station);
        var radius = Companion.SearchRadiusMeters(chargeMinutes);
        if (!await _cache.TryStartRefreshAsync(stationId, _clock.GetUtcNow().UtcDateTime, ct))
        {
            _logger.LogDebug("Places for station {StationId} are being or were just refreshed; skipped", stationId);
            return false;
        }

        var places = await _places.GetNearbyAsync(station.Location.Y, station.Location.X, radius, ct);
        var now = _clock.GetUtcNow().UtcDateTime;

        if (places is null)
        {
            await _cache.SaveFailureAsync(stationId, ProviderUnavailableError, now, ct);
            _logger.LogWarning("Could not refresh nearby places for station {StationId}; previous places kept", stationId);
            return false;
        }

        await _cache.SaveSuccessAsync(stationId, places, now, ct);
        if (places.Count == 0)
            _logger.LogInformation("No nearby places found for station {StationId}; checking again in {Hours} h",
                                   stationId, PlacesWarmup.EmptyStaleAfter.TotalHours);
        else
            _logger.LogInformation("Stored {Count} nearby places for station {StationId}", places.Count, stationId);
        return true;
    }
}
