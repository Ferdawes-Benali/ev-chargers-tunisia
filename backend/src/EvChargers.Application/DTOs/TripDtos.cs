namespace EvChargers.Application.DTOs;

public record TripPlanRequest(
    Guid VehicleId,
    double BatteryPercent,
    double OriginLat,
    double OriginLng,
    double DestLat,
    double DestLng
);

/// <param name="RoutePoints">Road route as [lat, lng].</param>
/// <param name="BatteryAtPoints">Battery % at each route point (same length as RoutePoints).</param>
/// <param name="LowBatteryPoint">[lat, lng] where the battery reaches the reserve, or null.</param>
/// <param name="ReachableStationIds">Chargers near the route that the driver reaches before the battery hits the reserve.</param>
/// <param name="RecommendedStop">Where to charge when the destination is out of reach, or null.</param>
/// <param name="ChargersAlongRouteCount">All chargers near the route ahead of the driver, reachable or not.</param>
public record TripPlanResult(
    double DistanceKm,
    double DurationMinutes,
    double RangeKm,
    double BatteryOnArrival,
    bool Reachable,
    double ShortfallKm,
    double MotorwayShare,
    double? TemperatureC,
    List<double[]> RoutePoints,
    List<double> BatteryAtPoints,
    double[]? LowBatteryPoint,
    List<Guid> ReachableStationIds,
    RecommendedStopDto? RecommendedStop,
    int ChargersAlongRouteCount
);

/// <param name="DistanceAlongKm">Road distance from the start to where the driver leaves the route.</param>
/// <param name="BatteryOnArrivalPercent">Battery % when passing that point of the route.</param>
public record RecommendedStopDto(
    Guid StationId,
    string Name,
    double DistanceAlongKm,
    double BatteryOnArrivalPercent,
    double Lat,
    double Lng
);

public enum TripPlanStatus { Ok, VehicleNotFound, RouteUnavailable }

public record TripPlanOutcome(TripPlanStatus Status, TripPlanResult? Plan = null);
