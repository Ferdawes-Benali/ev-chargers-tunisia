using EvChargers.Application.Common;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;

namespace EvChargers.Application.Services;

public class ReachEstimatorService : IReachEstimatorService
{
    private const double RoadFactor = 0.75; // straight-line reach ≈ 75% of road range

    private readonly IVehicleRepository _vehicles;
    private readonly IStationRepository _stations;
    private readonly IWeatherProvider _weather;

    public ReachEstimatorService(IVehicleRepository vehicles, IStationRepository stations, IWeatherProvider weather)
    {
        _vehicles = vehicles;
        _stations = stations;
        _weather = weather;
    }

    public async Task<ReachEstimateResult?> EstimateAsync(ReachEstimateRequest req, CancellationToken ct)
    {
        var vehicle = await _vehicles.GetByIdAsync(req.VehicleId, ct);
        if (vehicle is null) return null;

        // No route to inspect, so only the weather adjusts consumption
        var temperature = await _weather.GetCurrentTemperatureAsync(req.OriginLat, req.OriginLng, ct);
        var rangeKm = RangeCalculator.CalculateRangeKm(
            vehicle.BatteryKwh, vehicle.BaseConsumptionWhPerKm, vehicle.SocReservePercent,
            req.BatteryPercent, ConditionModel.GetMultiplier(0, temperature));

        // Stations within straight-line reach (reuses the PostGIS nearby query)
        var reachable = rangeKm > 0
            ? await _stations.GetNearbyAsync(req.OriginLat, req.OriginLng, rangeKm * RoadFactor, ct)
            : [];

        return new ReachEstimateResult(Math.Round(rangeKm, 1), reachable.Select(s => s.Id).ToList());
    }
}
