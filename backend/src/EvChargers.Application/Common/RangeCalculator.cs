namespace EvChargers.Application.Common;

public static class RangeCalculator
{
    /// <summary>
    /// Estimates driving range in km based on battery %, vehicle specs, and driving conditions.
    /// </summary>
    public static double CalculateRangeKm(
        double batteryKwh,
        double baseConsumptionWhPerKm,
        double socReservePercent,
        double batteryPercent,
        string drivingCondition)
    {
        var usableBatteryPercent = Math.Max(0, batteryPercent - socReservePercent);
        var usableKwh = batteryKwh * (usableBatteryPercent / 100.0);

        var conditionMultiplier = drivingCondition.ToLowerInvariant() switch
        {
            "highway" => 1.25, // highway driving consumes more energy per km
            "cold" => 1.35,    // cold weather significantly reduces efficiency
            _ => 1.0,          // city / default
        };

        var adjustedConsumptionWhPerKm = baseConsumptionWhPerKm * conditionMultiplier;

        if (adjustedConsumptionWhPerKm <= 0) return 0;

        return (usableKwh * 1000) / adjustedConsumptionWhPerKm;
    }
}