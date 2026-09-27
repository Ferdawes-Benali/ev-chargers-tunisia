namespace EvChargers.Application.Common;

/// <summary>A charging station to check against a route.</summary>
public record RouteStation(Guid Id, string Name, double Lat, double Lng);

/// <param name="NearestIndex">Index of the closest route point.</param>
/// <param name="DistanceAlongKm">Road distance from the start to that route point.</param>
/// <param name="DetourKm">Straight-line distance from that route point to the station.</param>
/// <param name="BatteryWhenPassingPercent">Battery % when the driver passes that route point.</param>
/// <param name="AheadOfStart">False when the driver would have to turn back at the start to reach it.</param>
/// <param name="OnTheWay">Ahead of the driver and reached before the battery hits the reserve.</param>
public record RouteStopEvaluation(
    RouteStation Station,
    int NearestIndex,
    double DistanceAlongKm,
    double DetourKm,
    double BatteryWhenPassingPercent,
    bool AheadOfStart,
    bool OnTheWay);

/// <param name="Recommended">The charging stop to suggest, or null when the trip is reachable or no station qualifies.</param>
public record RouteStopsResult(List<RouteStopEvaluation> Stations, RouteStopEvaluation? Recommended);

public static class RouteStops
{
    /// <summary>
    /// Finds which stations near a route the driver can use on the way, and recommends one stop if the trip needs it.
    /// </summary>
    /// <param name="routePoints">Route as [lat, lng].</param>
    /// <param name="batteryAtPoints">Battery % at each route point.</param>
    /// <param name="cumulativeKmAtPoints">Road distance from the start at each route point.</param>
    public static RouteStopsResult Evaluate(
        IReadOnlyList<double[]> routePoints,
        IReadOnlyList<double> batteryAtPoints,
        IReadOnlyList<double> cumulativeKmAtPoints,
        IEnumerable<RouteStation> stations,
        double reservePercent)
    {
        if (routePoints.Count == 0) return new RouteStopsResult([], null);

        var evaluations = stations.Select(station =>
        {
            var nearest = 0;
            var detour = double.MaxValue;
            for (var i = 0; i < routePoints.Count; i++)
            {
                var d = GeoUtils.HaversineDistanceKm(routePoints[i][0], routePoints[i][1], station.Lat, station.Lng);
                if (d < detour) { detour = d; nearest = i; }
            }

            var battery = batteryAtPoints[nearest];
            var ahead = !IsBehindStart(routePoints, nearest, station);
            return new RouteStopEvaluation(
                station, nearest, cumulativeKmAtPoints[nearest], detour, battery,
                ahead, OnTheWay: ahead && battery >= reservePercent);
        }).ToList();

        // The battery on arrival is the last point: a stop is only needed if it ends below the reserve
        var reachable = batteryAtPoints[^1] >= reservePercent;
        var recommended = reachable
            ? null
            : evaluations
                .Where(e => e.OnTheWay)
                // The furthest usable charger gets the driver closest to the destination; shorter detour breaks ties
                .OrderByDescending(e => e.DistanceAlongKm)
                .ThenBy(e => e.DetourKm)
                .FirstOrDefault();

        return new RouteStopsResult(evaluations, recommended);
    }

    /// <summary>
    /// True when the station is closest to the start and lies on the far side of it,
    /// i.e. the driver would have to turn back to reach it.
    /// </summary>
    private static bool IsBehindStart(IReadOnlyList<double[]> routePoints, int nearestIndex, RouteStation station)
    {
        if (nearestIndex != 0 || routePoints.Count < 2) return false;

        // Local flat projection around the start (fine at a few km)
        var start = routePoints[0];
        var next = routePoints[1];
        var cosLat = Math.Cos(start[0] * Math.PI / 180);
        var routeX = (next[1] - start[1]) * cosLat;
        var routeY = next[0] - start[0];
        var stationX = (station.Lng - start[1]) * cosLat;
        var stationY = station.Lat - start[0];

        return routeX * stationX + routeY * stationY < 0;
    }
}
