namespace EvChargers.Application.DTOs;

public record ReachEstimateRequest(
    Guid VehicleId,
    double BatteryPercent,
    double OriginLat,
    double OriginLng
);

public record ReachEstimateResult(
    double RangeKm,
    List<Guid> ReachableStationIds
);
