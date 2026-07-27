namespace EvChargers.Application.DTOs;

public record ReachEstimateRequest(
    Guid VehicleId,
    double BatteryPercent,
    string DrivingCondition, // "city" | "highway" | "cold"
    double OriginLat,
    double OriginLng,
    double? DestLat,
    double? DestLng
);

public record ReachEstimateResult(
    double RangeKm,
    double? BatteryPercentOnArrival,
    bool? DestinationReachable,
    string? IsochroneGeoJson,
    List<Guid> ReachableStationIds
);