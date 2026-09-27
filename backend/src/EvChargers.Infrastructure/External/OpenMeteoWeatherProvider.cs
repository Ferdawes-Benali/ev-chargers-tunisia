using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using EvChargers.Application.Interfaces;

namespace EvChargers.Infrastructure.External;

public class OpenMeteoWeatherProvider : IWeatherProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OpenMeteoWeatherProvider> _logger;

    public OpenMeteoWeatherProvider(HttpClient http, IMemoryCache cache, ILogger<OpenMeteoWeatherProvider> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        _http.BaseAddress = new Uri("https://api.open-meteo.com/");
    }

    public async Task<double?> GetCurrentTemperatureAsync(double lat, double lng, CancellationToken ct)
    {
        // ~10 km grid: plenty for air temperature, and keeps the cache small
        var roundedLat = Math.Round(lat, 1);
        var roundedLng = Math.Round(lng, 1);
        var cacheKey = FormattableString.Invariant($"weather:{roundedLat},{roundedLng}");
        if (_cache.TryGetValue(cacheKey, out double cached))
            return cached;

        try
        {
            var url = FormattableString.Invariant(
                $"v1/forecast?latitude={roundedLat}&longitude={roundedLng}&current=temperature_2m");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Open-Meteo request failed with status {Status}: {Body}", response.StatusCode, errorBody);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("current", out var current)
                || !current.TryGetProperty("temperature_2m", out var temp)
                || !temp.TryGetDouble(out var temperature))
            {
                _logger.LogWarning("Open-Meteo response had no current temperature");
                return null;
            }

            _cache.Set(cacheKey, temperature, CacheDuration);
            return temperature;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open-Meteo request threw an exception");
            return null;
        }
    }
}
