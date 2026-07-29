namespace EvChargers.Application.Common;

/// <summary>Rough charging duration for a typical car, used to size the "while you charge" walk.</summary>
public static class ChargingTime
{
    /// <summary>Typical 20 → 80 % top-up of a ~60 kWh battery.</summary>
    public const double TypicalEnergyKwh = 36;
    public const int AcMaxKw = 22;

    private const double AcEfficiency = 0.9;
    // DC power drops as the battery fills, and most cars accept ~100 kW at most
    private const double DcAverageFactor = 0.75;
    private const double DcCarLimitKw = 100;

    /// <summary>Minutes rounded to the nearest 5, or null when the station has no usable connector.</summary>
    public static int? EstimateMinutes(int? maxPowerKw)
    {
        if (maxPowerKw is not > 0) return null;

        var effectiveKw = maxPowerKw.Value <= AcMaxKw
            ? maxPowerKw.Value * AcEfficiency
            : Math.Min(maxPowerKw.Value, DcCarLimitKw) * DcAverageFactor;

        var minutes = TypicalEnergyKwh / effectiveKw * 60;
        return (int)(Math.Round(minutes / 5, MidpointRounding.AwayFromZero) * 5);
    }
}
