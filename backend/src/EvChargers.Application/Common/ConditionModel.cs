namespace EvChargers.Application.Common;

/// <summary>Turns detected driving conditions into an energy consumption multiplier.</summary>
public static class ConditionModel
{
    public static double GetMultiplier(double motorwayShare, double? temperatureC)
    {
        // Higher speeds on motorways cost up to 25% more energy per km
        var motorwayFactor = 1 + 0.25 * Math.Clamp(motorwayShare, 0, 1);

        // Cold batteries and cabin heating reduce efficiency; unknown weather is treated as mild
        var coldFactor = temperatureC switch
        {
            < 5 => 1.35,
            < 15 => 1.15,
            _ => 1.0,
        };

        return motorwayFactor * coldFactor;
    }
}
