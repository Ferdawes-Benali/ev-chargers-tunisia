namespace EvChargers.Application.Interfaces;

public interface IWeatherProvider
{
    /// <summary>Current air temperature in °C, or null if unavailable.</summary>
    Task<double?> GetCurrentTemperatureAsync(double lat, double lng, CancellationToken ct);
}
