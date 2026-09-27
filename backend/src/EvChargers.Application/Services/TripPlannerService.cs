using EvChargers.Application.Common;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;

namespace EvChargers.Application.Services;

public class TripPlannerService : ITripPlannerService
{
    private const double RouteBufferKm = 5; // chargers further than this from the road aren't "on the way"

    private readonly IVehicleRepository _vehicles;
    private readonly IStationRepository _stations;
    private readonly IRoutingProvider _routing;
    private readonly IWeatherProvider _weather;

    public TripPlannerService(IVehicleRepository vehicles, IStationRepository stations, IRoutingProvider routing, IWeatherProvider weather)
    {
        _vehicles = vehicles;
        _stations = stations;
        _routing = routing;
        _weather = weather;
    }

    public async Task<TripPlanOutcome> PlanAsync(TripPlanRequest req, CancellationToken ct)
    {
        var vehicle = await _vehicles.GetByIdAsync(req.VehicleId, ct);
        if (vehicle is null) return new TripPlanOutcome(TripPlanStatus.VehicleNotFound);

        // Both are external HTTP calls: run them together
        var routeTask = _routing.GetRouteAsync(req.OriginLat, req.OriginLng, req.DestLat, req.DestLng, ct);
        var weatherTask = _weather.GetCurrentTemperatureAsync(req.OriginLat, req.OriginLng, ct);
        await Task.WhenAll(routeTask, weatherTask);

        var route = await routeTask;
        if (route is null || route.Points.Count < 2) return new TripPlanOutcome(TripPlanStatus.RouteUnavailable);
        var temperature = await weatherTask;

        var multiplier = ConditionModel.GetMultiplier(route.MotorwayShare, temperature);
        var rangeKm = RangeCalculator.CalculateRangeKm(
            vehicle.BatteryKwh, vehicle.BaseConsumptionWhPerKm, vehicle.SocReservePercent,
            req.BatteryPercent, multiplier);

        var energy = RouteEnergy.Compute(route.Points, req.BatteryPercent, vehicle.SocReservePercent, rangeKm, route.DistanceKm);

        // Chargers near the road (filtered by PostGIS), then which ones the battery reaches
        var nearRoute = rangeKm > 0
            ? await _stations.GetNearRouteAsync(energy.Points.Select(p => (p[0], p[1])).ToList(), RouteBufferKm, ct)
            : [];
        var stops = RouteStops.Evaluate(
            energy.Points, energy.BatteryAtPoints, energy.CumulativeKm,
            nearRoute.Select(s => new RouteStation(s.Id, s.Name, s.Location.Y, s.Location.X)),
            vehicle.SocReservePercent);
        var recommended = stops.Recommended;

        return new TripPlanOutcome(TripPlanStatus.Ok, new TripPlanResult(
            DistanceKm: Math.Round(route.DistanceKm, 1),
            DurationMinutes: Math.Round(route.DurationMinutes),
            RangeKm: Math.Round(rangeKm, 1),
            BatteryOnArrival: Math.Round(energy.BatteryOnArrival, 1),
            Reachable: energy.ShortfallKm <= 0,
            ShortfallKm: Math.Round(energy.ShortfallKm, 1),
            MotorwayShare: Math.Round(route.MotorwayShare, 2),
            TemperatureC: temperature is null ? null : Math.Round(temperature.Value),
            RoutePoints: energy.Points,
            BatteryAtPoints: energy.BatteryAtPoints.Select(b => Math.Round(b, 1)).ToList(),
            LowBatteryPoint: energy.LowBatteryPoint,
            ReachableStationIds: stops.Stations.Where(s => s.OnTheWay).Select(s => s.Station.Id).ToList(),
            RecommendedStop: recommended is null ? null : new RecommendedStopDto(
                recommended.Station.Id,
                recommended.Station.Name,
                Math.Round(recommended.DistanceAlongKm, 1),
                Math.Round(recommended.BatteryWhenPassingPercent, 1),
                recommended.Station.Lat,
                recommended.Station.Lng),
            ChargersAlongRouteCount: stops.Stations.Count(s => s.AheadOfStart)));
    }
}
