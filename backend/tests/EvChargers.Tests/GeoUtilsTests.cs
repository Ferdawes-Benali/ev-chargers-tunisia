using FluentAssertions;
using EvChargers.Application.Common;
using Xunit;

namespace EvChargers.Tests;

public class GeoUtilsTests
{
    [Fact]
    public void Distance_between_Tunis_and_Sfax_is_approximately_correct()
    {
        var distance = GeoUtils.HaversineDistanceKm(36.8065, 10.1815, 34.7406, 10.7603);
        distance.Should().BeApproximately(235.6, 5);
    }

    [Fact]
    public void Distance_between_identical_points_is_zero()
    {
        var distance = GeoUtils.HaversineDistanceKm(36.8065, 10.1815, 36.8065, 10.1815);
        distance.Should().Be(0);
    }
}