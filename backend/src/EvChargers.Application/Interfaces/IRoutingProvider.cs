namespace EvChargers.Application.Interfaces;

/// <param name="Points">Route polyline as [lat, lng].</param>
/// <param name="MotorwayShare">Share of the distance on motorways, 0..1.</param>
public record RouteData(double DistanceKm, double DurationMinutes, List<double[]> Points, double MotorwayShare);

public interface IRoutingProvider
{
    /// <summary>Road route between two points, or null if routing is unavailable.</summary>
    Task<RouteData?> GetRouteAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct);
}
