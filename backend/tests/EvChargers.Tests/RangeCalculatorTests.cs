using FluentAssertions;
using EvChargers.Application.Common;
using Xunit;

namespace EvChargers.Tests;

public class RangeCalculatorTests
{
    [Fact]
    public void City_driving_computes_expected_range()
    {
        // Tesla-like: 60 kWh, 150 Wh/km, reserve 5%, battery 75%
        // usable = 60 * (75-5)/100 = 42 kWh; range = 42*1000/150 = 280 km
        var range = RangeCalculator.CalculateRangeKm(60, 150, 5, 75, "city");
        range.Should().BeApproximately(280, 0.1);
    }

    [Fact]
    public void Highway_driving_reduces_range_compared_to_city()
    {
        var cityRange = RangeCalculator.CalculateRangeKm(60, 150, 5, 75, "city");
        var highwayRange = RangeCalculator.CalculateRangeKm(60, 150, 5, 75, "highway");
        highwayRange.Should().BeLessThan(cityRange);
    }

    [Fact]
    public void Cold_weather_reduces_range_more_than_highway()
    {
        var highwayRange = RangeCalculator.CalculateRangeKm(60, 150, 5, 75, "highway");
        var coldRange = RangeCalculator.CalculateRangeKm(60, 150, 5, 75, "cold");
        coldRange.Should().BeLessThan(highwayRange);
    }

    [Fact]
    public void Battery_at_or_below_reserve_gives_zero_range()
    {
        var range = RangeCalculator.CalculateRangeKm(60, 150, 10, 10, "city");
        range.Should().Be(0);
    }
}