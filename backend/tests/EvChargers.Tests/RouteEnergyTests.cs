using FluentAssertions;
using EvChargers.Application.Common;
using Xunit;

namespace EvChargers.Tests;

public class RouteEnergyTests
{
    // Straight line north along a meridian: 0.1° latitude ≈ 11.1 km per leg, ≈ 22.2 km in total
    private static readonly List<double[]> Line = [[36.0, 10.0], [36.1, 10.0], [36.2, 10.0]];
    private static readonly double LineKm = GeoUtils.HaversineDistanceKm(36.0, 10.0, 36.2, 10.0);

    [Fact]
    public void Battery_decreases_along_the_route()
    {
        var result = RouteEnergy.Compute(Line, 80, 10, 300);

        result.BatteryAtPoints.Should().HaveCount(Line.Count);
        result.BatteryAtPoints[0].Should().Be(80);
        result.BatteryAtPoints.Should().BeInDescendingOrder();
        result.BatteryOnArrival.Should().BeLessThan(80);
    }

    [Fact]
    public void No_low_battery_point_when_range_is_long()
    {
        var result = RouteEnergy.Compute(Line, 80, 10, 300);

        result.LowBatteryPoint.Should().BeNull();
        result.ShortfallKm.Should().Be(0);
        // 70 usable % over 300 km
        result.BatteryOnArrival.Should().BeApproximately(80 - LineKm * 70 / 300, 0.01);
    }

    [Fact]
    public void Low_battery_point_is_found_when_range_is_short()
    {
        // Reserve reached after 15 km: on the second leg, a bit past the middle point
        var result = RouteEnergy.Compute(Line, 40, 10, 15);

        result.LowBatteryPoint.Should().NotBeNull();
        result.LowBatteryPoint![0].Should().BeInRange(36.1, 36.2);
        result.LowBatteryPoint[1].Should().BeApproximately(10.0, 1e-9);

        // The low point is inserted into the route so the map can split the line there
        result.Points.Should().HaveCount(Line.Count + 1);
        result.Points.Should().ContainEquivalentOf(result.LowBatteryPoint);
        result.BatteryAtPoints.Should().HaveCount(result.Points.Count);
        result.BatteryAtPoints[result.Points.IndexOf(result.LowBatteryPoint)].Should().BeApproximately(10, 0.01);
    }

    [Fact]
    public void Shortfall_is_the_distance_beyond_the_range()
    {
        var result = RouteEnergy.Compute(Line, 40, 10, 15);

        result.ShortfallKm.Should().BeApproximately(LineKm - 15, 0.01);
        result.BatteryOnArrival.Should().BeLessThan(10);
    }

    [Fact]
    public void Road_distance_scales_the_polyline()
    {
        // The real road is 30 km although the polyline measures ≈ 22.2 km
        var result = RouteEnergy.Compute(Line, 40, 10, 25, roadDistanceKm: 30);

        result.ShortfallKm.Should().BeApproximately(5, 0.01);
        result.BatteryOnArrival.Should().BeApproximately(40 - 30 * 30.0 / 25, 0.01);
    }
}
