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

    private readonly IStationRepository _stations;
    private readonly IPlacesProvider _places;
    private readonly IRoutingProvider _routing;
    private readonly TimeProvider _clock;

    public CompanionService(IStationRepository stations, IPlacesProvider places, IRoutingProvider routing, TimeProvider clock)
    {
        _stations = stations;
        _places = places;
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

        var raw = await NearbyAsync(station, chargeMinutes, ct);
        if (raw is null)
            return new CompanionResultDto(chargeMinutes, maxPowerKw, backBy, [], [], Source, Unavailable: true);

        var places = Companion.Build(station.Location.Y, station.Location.X, raw, chargeMinutes, localNow);
        return new CompanionResultDto(chargeMinutes, maxPowerKw, backBy, Companion.Picks(places, localNow), places,
                                      Source, Unavailable: false);
    }

    public async Task<WalkingRouteDto?> GetWalkingRouteAsync(Guid stationId, string placeId, CancellationToken ct)
    {
        var station = await _stations.GetByIdAsync(stationId, ct);
        if (station is null) return null;

        // Same radius as the list, so this is served from the places cache
        var (_, chargeMinutes) = ChargeOf(station);
        var place = (await NearbyAsync(station, chargeMinutes, ct))?.FirstOrDefault(p => p.Id == placeId);
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

    private static (int? MaxPowerKw, int? ChargeMinutes) ChargeOf(Station station)
    {
        int? maxPowerKw = station.Connectors.Count > 0 ? station.Connectors.Max(c => c.PowerKw) : null;
        return (maxPowerKw, ChargingTime.EstimateMinutes(maxPowerKw));
    }

    private Task<List<RawPlace>?> NearbyAsync(Station station, int? chargeMinutes, CancellationToken ct) =>
        _places.GetNearbyAsync(station.Location.Y, station.Location.X, Companion.SearchRadiusMeters(chargeMinutes), ct);
}
