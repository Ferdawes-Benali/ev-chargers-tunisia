namespace EvChargers.Application.Common;

public static class Walking
{
    /// <summary>Streets are never straight: walked distance ≈ 1.25 × straight-line distance.</summary>
    public const double DetourFactor = 1.25;
    /// <summary>Relaxed walking pace in meters per minute (~4.8 km/h).</summary>
    public const double MetersPerMinute = 80;

    public static int DistanceMeters(double lat1, double lng1, double lat2, double lng2) =>
        (int)Math.Round(GeoUtils.HaversineDistanceKm(lat1, lng1, lat2, lng2) * 1000);

    public static int WalkMinutes(double straightLineMeters) =>
        (int)Math.Ceiling(straightLineMeters * DetourFactor / MetersPerMinute);

    /// <summary>Straight-line radius reachable in the given walking minutes.</summary>
    public static double RadiusMeters(int walkMinutes) => walkMinutes * MetersPerMinute / DetourFactor;
}
