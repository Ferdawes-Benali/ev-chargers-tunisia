using FluentAssertions;
using EvChargers.Application.Common;
using Xunit;

namespace EvChargers.Tests;

public class ConditionModelTests
{
    [Fact]
    public void Local_roads_in_mild_weather_have_no_penalty()
    {
        ConditionModel.GetMultiplier(0, 22).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void Unknown_temperature_is_treated_as_mild()
    {
        ConditionModel.GetMultiplier(0, null).Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void Full_motorway_adds_25_percent()
    {
        ConditionModel.GetMultiplier(1, 20).Should().BeApproximately(1.25, 1e-9);
    }

    [Theory]
    [InlineData(2, 1.35)]
    [InlineData(10, 1.15)]
    [InlineData(15, 1.0)]
    public void Cold_weather_factor_depends_on_temperature(double temperature, double expected)
    {
        ConditionModel.GetMultiplier(0, temperature).Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void Motorway_and_cold_factors_multiply()
    {
        // half motorway (1.125) in cold weather (1.35)
        ConditionModel.GetMultiplier(0.5, 0).Should().BeApproximately(1.125 * 1.35, 1e-9);
    }
}
