using FluentAssertions;
using EvChargers.Application.Common;
using Xunit;

namespace EvChargers.Tests;

public class RouteStopsTests
{
    // Straight route north along a meridian: 5 points, 0.1° latitude (≈ 11.1 km) apart, ≈ 44.5 km in total
    private static readonly List<double[]> Route = [[36.0, 10.0], [36.1, 10.0], [36.2, 10.0], [36.3, 10.0], [36.4, 10.0]];
    private static readonly List<double> Km = Route.Select(p => GeoUtils.HaversineDistanceKm(36.0, 10.0, p[0], 10.0)).ToList();

    private const double Reserve = 10;

    // Leaves with 40%, reaches the 10% reserve at the third point (≈ 22 km): the trip is NOT reachable
    private static readonly List<double> ShortBattery = [40, 25, 10, 0, 0];
    // Plenty of battery: arrives with 60%
    private static readonly List<double> LongBattery = [80, 75, 70, 65, 60];

    private static RouteStation Station(string name, double lat, double lng) => new(Guid.NewGuid(), name, lat, lng);

    [Fact]
    public void Station_behind_the_origin_is_excluded()
    {
        var behind = Station("Behind", 35.97, 10.0); // ≈ 3 km south of the start, route goes north

        var result = RouteStops.Evaluate(Route, ShortBattery, Km, [behind], Reserve);

        var evaluation = result.Stations.Single();
        evaluation.AheadOfStart.Should().BeFalse();
        evaluation.OnTheWay.Should().BeFalse();
        result.Recommended.Should().BeNull();
    }

    [Fact]
    public void Station_beside_the_start_is_on_the_way()
    {
        var beside = Station("Beside", 36.0, 10.02); // ≈ 1.8 km east of the start

        var result = RouteStops.Evaluate(Route, ShortBattery, Km, [beside], Reserve);

        result.Stations.Single().OnTheWay.Should().BeTrue();
    }

    [Fact]
    public void Station_after_the_low_battery_point_is_excluded()
    {
        var tooFar = Station("Too far", 36.3, 10.01);

        var result = RouteStops.Evaluate(Route, ShortBattery, Km, [tooFar], Reserve);

        var evaluation = result.Stations.Single();
        evaluation.NearestIndex.Should().Be(3);
        evaluation.BatteryWhenPassingPercent.Should().Be(0);
        evaluation.OnTheWay.Should().BeFalse();
        result.Recommended.Should().BeNull();
    }

    [Fact]
    public void Evaluation_reports_distance_detour_and_battery()
    {
        var station = Station("Mid", 36.1, 10.01);

        var evaluation = RouteStops.Evaluate(Route, ShortBattery, Km, [station], Reserve).Stations.Single();

        evaluation.NearestIndex.Should().Be(1);
        evaluation.DistanceAlongKm.Should().BeApproximately(11.1, 0.1);
        evaluation.DetourKm.Should().BeApproximately(0.9, 0.1);
        evaluation.BatteryWhenPassingPercent.Should().Be(25);
    }

    [Fact]
    public void Recommended_stop_is_the_furthest_reachable_station()
    {
        var early = Station("Early", 36.05, 10.01);
        var furthest = Station("Furthest", 36.2, 10.01); // at the reserve point: still reachable
        var tooFar = Station("Too far", 36.3, 10.01);

        var result = RouteStops.Evaluate(Route, ShortBattery, Km, [early, furthest, tooFar], Reserve);

        result.Recommended.Should().NotBeNull();
        result.Recommended!.Station.Name.Should().Be("Furthest");
        result.Stations.Where(s => s.OnTheWay).Select(s => s.Station.Name)
            .Should().BeEquivalentTo(["Early", "Furthest"]);
    }

    [Fact]
    public void No_recommendation_when_the_destination_is_reachable()
    {
        var station = Station("Mid", 36.2, 10.01);

        var result = RouteStops.Evaluate(Route, LongBattery, Km, [station], Reserve);

        result.Stations.Single().OnTheWay.Should().BeTrue();
        result.Recommended.Should().BeNull();
    }

    [Fact]
    public void No_recommendation_when_no_station_qualifies()
    {
        var result = RouteStops.Evaluate(Route, ShortBattery, Km, [], Reserve);

        result.Stations.Should().BeEmpty();
        result.Recommended.Should().BeNull();
    }
}
