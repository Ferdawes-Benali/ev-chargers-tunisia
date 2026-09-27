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
        var conditionMultiplier = drivingCondition.ToLowerInvariant() switch
        {
            "highway" => 1.25, // highway driving consumes more energy per km
            "cold" => 1.35,    // cold weather significantly reduces efficiency
            _ => 1.0,          // city / default
        };

        return CalculateRangeKm(batteryKwh, baseConsumptionWhPerKm, socReservePercent, batteryPercent, conditionMultiplier);
    }

    /// <summary>
    /// Estimates driving range in km with an explicit consumption multiplier (see <see cref="ConditionModel"/>).
    /// </summary>
    public static double CalculateRangeKm(
        double batteryKwh,
        double baseConsumptionWhPerKm,
        double socReservePercent,
        double batteryPercent,
        double consumptionMultiplier)
    {
        var usableBatteryPercent = Math.Max(0, batteryPercent - socReservePercent);
        var usableKwh = batteryKwh * (usableBatteryPercent / 100.0);

        var adjustedConsumptionWhPerKm = baseConsumptionWhPerKm * consumptionMultiplier;

        if (adjustedConsumptionWhPerKm <= 0) return 0;

        return (usableKwh * 1000) / adjustedConsumptionWhPerKm;
    }
}
