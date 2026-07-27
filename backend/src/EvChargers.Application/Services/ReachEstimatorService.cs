using EvChargers.Application.Common;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;

namespace EvChargers.Application.Services;

public class ReachEstimatorService : IReachEstimatorService
{
    private const double RoadFactor = 0.75; // straight-line reach ≈ 75% of road range

    private readonly IVehicleRepository _vehicles;
    private readonly IStationRepository _stations;
    private readonly IOrsIsochroneProvider _ors;

    public ReachEstimatorService(IVehicleRepository vehicles, IStationRepository stations, IOrsIsochroneProvider ors)
    {
        _vehicles = vehicles;
        _stations = stations;
        _ors = ors;
    }

    public async Task<ReachEstimateResult?> EstimateAsync(ReachEstimateRequest req, CancellationToken ct)
    {
        var vehicle = await _vehicles.GetByIdAsync(req.VehicleId, ct);
        if (vehicle is null) return null;

        var rangeKm = RangeCalculator.CalculateRangeKm(
            vehicle.BatteryKwh, vehicle.BaseConsumptionWhPerKm, vehicle.SocReservePercent,
            req.BatteryPercent, req.DrivingCondition);

        // Stations within straight-line reach (reuses the PostGIS nearby query)
        var reachable = rangeKm > 0
            ? await _stations.GetNearbyAsync(req.OriginLat, req.OriginLng, rangeKm * RoadFactor, ct)
            : [];

        // Destination: battery on arrival
        double? arrival = null;
        bool? destReachable = null;
        if (req.DestLat.HasValue && req.DestLng.HasValue && rangeKm > 0)
        {
            var straightKm = GeoUtils.HaversineDistanceKm(req.OriginLat, req.OriginLng, req.DestLat.Value, req.DestLng.Value);
            var roadKm = straightKm / RoadFactor;
            var usablePercent = req.BatteryPercent - vehicle.SocReservePercent;
            var percentPerKm = usablePercent / rangeKm;
            arrival = Math.Round(req.BatteryPercent - roadKm * percentPerKm, 1);
            destReachable = arrival >= vehicle.SocReservePercent;
        }

        // Road-based reachable area (null if ORS fails — frontend falls back to the circle)
        var isochrone = rangeKm > 0
            ? await _ors.GetIsochroneGeoJsonAsync(req.OriginLat, req.OriginLng, rangeKm * 1000, ct)
            : null;

        return new ReachEstimateResult(
            Math.Round(rangeKm, 1), arrival, destReachable, isochrone,
            reachable.Select(s => s.Id).ToList());
    }
}