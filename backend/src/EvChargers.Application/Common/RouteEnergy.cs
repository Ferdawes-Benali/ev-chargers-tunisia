namespace EvChargers.Application.Common;

/// <param name="Points">Route points as [lat, lng]; includes the point where the battery reaches the reserve, if any.</param>
/// <param name="BatteryAtPoints">Battery % at each point (same length as Points, never below 0).</param>
/// <param name="LowBatteryPoint">[lat, lng] where the battery reaches the reserve, or null if it never does.</param>
public record RouteEnergyResult(
    List<double[]> Points,
    List<double> BatteryAtPoints,
    double[]? LowBatteryPoint,
    double BatteryOnArrival,
    double ShortfallKm);

public static class RouteEnergy
{
    /// <summary>
    /// Walks a route and estimates the battery level along it, assuming constant consumption per km.
    /// </summary>
    /// <param name="points">Route polyline as [lat, lng].</param>
    /// <param name="rangeKm">Distance the car can drive before reaching the reserve.</param>
    /// <param name="roadDistanceKm">
    /// Real road distance. When given, polyline distances are scaled to it, because a simplified
    /// polyline is always a bit shorter than the road it follows.
    /// </param>
    public static RouteEnergyResult Compute(
        IReadOnlyList<double[]> points,
        double batteryPercent,
        double reservePercent,
        double rangeKm,
        double? roadDistanceKm = null)
    {
        if (points.Count == 0)
            return new RouteEnergyResult([], [], null, batteryPercent, 0);

        // Cumulative distance at each point
        var cumulative = new double[points.Count];
        for (var i = 1; i < points.Count; i++)
            cumulative[i] = cumulative[i - 1] + GeoUtils.HaversineDistanceKm(
                points[i - 1][0], points[i - 1][1], points[i][0], points[i][1]);

        var polylineKm = cumulative[^1];
        var totalKm = roadDistanceKm ?? polylineKm;
        var scale = polylineKm > 0 ? totalKm / polylineKm : 0;

        // Already at or below the reserve: any driving goes below it
        if (rangeKm <= 0)
        {
            return new RouteEnergyResult(
                points.ToList(),
                Enumerable.Repeat(Math.Max(0, batteryPercent), points.Count).ToList(),
                points[0],
                Math.Max(0, batteryPercent),
                totalKm);
        }

        var percentPerKm = (batteryPercent - reservePercent) / rangeKm;
        double BatteryAt(double km) => Math.Max(0, batteryPercent - km * percentPerKm);

        var outPoints = new List<double[]>(points.Count + 1);
        var battery = new List<double>(points.Count + 1);
        double[]? lowPoint = null;

        for (var i = 0; i < points.Count; i++)
        {
            var km = cumulative[i] * scale;

            // Insert the exact point where the reserve is reached, between the previous point and this one
            if (lowPoint is null && km > rangeKm && i > 0)
            {
                var prevKm = cumulative[i - 1] * scale;
                var t = (rangeKm - prevKm) / (km - prevKm);
                lowPoint =
                [
                    points[i - 1][0] + t * (points[i][0] - points[i - 1][0]),
                    points[i - 1][1] + t * (points[i][1] - points[i - 1][1]),
                ];
                outPoints.Add(lowPoint);
                battery.Add(BatteryAt(rangeKm));
            }

            outPoints.Add(points[i]);
            battery.Add(BatteryAt(km));
        }

        return new RouteEnergyResult(
            outPoints,
            battery,
            lowPoint,
            BatteryAt(totalKm),
            Math.Max(0, totalKm - rangeKm));
    }
}
