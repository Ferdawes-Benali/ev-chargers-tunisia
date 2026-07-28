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
    List<Guid> ReachableStationIds
);

public enum TripPlanStatus { Ok, VehicleNotFound, RouteUnavailable }

public record TripPlanOutcome(TripPlanStatus Status, TripPlanResult? Plan = null);
