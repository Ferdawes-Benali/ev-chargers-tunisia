namespace EvChargers.Application.Interfaces;

/// <param name="Points">Route polyline as [lat, lng].</param>
/// <param name="MotorwayShare">Share of the distance on motorways, 0..1.</param>
public record RouteData(double DistanceKm, double DurationMinutes, List<double[]> Points, double MotorwayShare);

/// <summary>ORS routing profiles we use.</summary>
public static class RoutingProfiles
{
    public const string Car = "driving-car";
    public const string Foot = "foot-walking";
}

public interface IRoutingProvider
{
    /// <summary>Route between two points (by car unless another profile is given), or null if routing is unavailable.</summary>
    Task<RouteData?> GetRouteAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct,
                                   string profile = RoutingProfiles.Car);
}
