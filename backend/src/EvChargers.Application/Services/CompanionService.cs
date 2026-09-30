using EvChargers.Application.Common;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;
using EvChargers.Domain.Entities;

namespace EvChargers.Application.Services;

public class CompanionService : ICompanionService
{
    public const string Source = "OpenStreetMap";
    /// <summary>Maximum number of points used to display walking routes on the map.</summary>
    public const int MaxRoutePoints = 200;

    /// <summary>With nothing stored, "preparing" turns into "unavailable" after this many failed attempts in a row.</summary>
    public const int UnavailableAfterFailures = 3;

    private readonly IStationRepository _stations;
    private readonly IPlacesCacheRepository _cache;
    private readonly IPlacesRefreshQueue _refreshQueue;
    private readonly IRoutingProvider _routing;
    private readonly TimeProvider _clock;

    // No IPlacesProvider on purpose: places come only from the stored cache, filled in the background
    // (PlacesCacheRefresher), so a response never waits on Overpass.
    public CompanionService(IStationRepository stations, IPlacesCacheRepository cache, IPlacesRefreshQueue refreshQueue,
        IRoutingProvider routing, TimeProvider clock)
    {
        _stations = stations;
        _cache = cache;
        _refreshQueue = refreshQueue;
        _routing = routing;
        _clock = clock;
    }

    public async Task<CompanionResultDto?> GetAsync(Guid stationId, CancellationToken ct)
    {
        var station = await _stations.GetByIdAsync(stationId, ct);
        if (station is null) return null;

        var (maxPowerKw, chargeMinutes) = ChargeOf(station);
        var localNow = TunisiaTime.ToLocal(_clock.GetUtcNow());
        var backBy = Companion.BackBy(localNow, chargeMinutes);

        var cached = await _cache.GetAsync(stationId, ct);
        if (cached?.Places is null)
        {
            // Failed before with nothing stored: still "preparing" while retries are pending, "unavailable" after
            // several failures in a row. Either way, ask again once the backoff delay has passed (same as the warmup).
            if (cached?.LastError is not null)
            {
                if (_clock.GetUtcNow().UtcDateTime - cached.LastAttemptAt >= PlacesWarmup.RetryDelay(cached.AttemptCount))
                    _refreshQueue.Request(stationId);
                var unavailable = cached.AttemptCount >= UnavailableAfterFailures;
                return new CompanionResultDto(chargeMinutes, maxPowerKw, backBy, [], [], Source, Unavailable: unavailable,
                                              unavailable ? CompanionStatuses.Unavailable : CompanionStatuses.Preparing);
            }

            _refreshQueue.Request(stationId);
            return new CompanionResultDto(chargeMinutes, maxPowerKw, backBy, [], [], Source,
                                          Unavailable: false, CompanionStatuses.Preparing);
        }

        var places = Companion.Build(station.Location.Y, station.Location.X, cached.Places, chargeMinutes, localNow);
        return new CompanionResultDto(chargeMinutes, maxPowerKw, backBy, Companion.Picks(places, localNow), places,
                                      Source, Unavailable: false, CompanionStatuses.Ready, cached.FetchedAt);
    }

    public async Task<WalkingRouteDto?> GetWalkingRouteAsync(Guid stationId, string placeId, CancellationToken ct)
    {
        var station = await _stations.GetByIdAsync(stationId, ct);
        if (station is null) return null;

        // The place must be one the companion listed, i.e. in the stored places
        var place = (await _cache.GetAsync(stationId, ct))?.Places?.FirstOrDefault(p => p.Id == placeId);
        if (place is null) return null;

        var (fromLat, fromLng) = (station.Location.Y, station.Location.X);
        var route = await _routing.GetRouteAsync(fromLat, fromLng, place.Lat, place.Lng, ct, RoutingProfiles.Foot);
        if (route is null || route.Points.Count < 2)
        {
            // Honest fallback: straight-line estimate, no drawn path
            var meters = Walking.DistanceMeters(fromLat, fromLng, place.Lat, place.Lng);
            return new WalkingRouteDto((int)Math.Round(meters * Walking.DetourFactor), Walking.WalkMinutes(meters), [], Estimated: true);
        }

        return new WalkingRouteDto(
            (int)Math.Round(route.DistanceKm * 1000),
            Math.Max(1, (int)Math.Ceiling(route.DurationMinutes)),
            Polyline.Downsample(route.Points, MaxRoutePoints),
            Estimated: false);
    }

    internal static (int? MaxPowerKw, int? ChargeMinutes) ChargeOf(Station station)
    {
        int? maxPowerKw = station.Connectors.Count > 0 ? station.Connectors.Max(c => c.PowerKw) : null;
        return (maxPowerKw, ChargingTime.EstimateMinutes(maxPowerKw));
    }
}
