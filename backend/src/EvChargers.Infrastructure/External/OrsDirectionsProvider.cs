using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using EvChargers.Application.Interfaces;

namespace EvChargers.Infrastructure.External;

public class OrsDirectionsProvider : IRoutingProvider
{
    private const int MaxPoints = 400;
    private const int HighwayBit = 1; // ORS waycategory is a bit field: 1 = highway (motorway/trunk)
    // Footpaths hardly change and walking routes are short and repeated: spare the free ORS quota
    private static readonly TimeSpan WalkingCacheDuration = TimeSpan.FromHours(24);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OrsDirectionsProvider> _logger;
    private readonly string? _apiKey;

    public OrsDirectionsProvider(HttpClient http, IMemoryCache cache, IConfiguration config, ILogger<OrsDirectionsProvider> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        _apiKey = config["OrsApiKey"];
        _http.BaseAddress = new Uri("https://api.openrouteservice.org/");
    }

    public async Task<RouteData?> GetRouteAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct,
                                                string profile = RoutingProfiles.Car)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("ORS API key not configured; skipping directions.");
            return null;
        }

        // Only walking routes are cached; driving routes behave exactly as before
        var cacheKey = profile == RoutingProfiles.Foot
            ? FormattableString.Invariant($"route:{profile}:{Math.Round(fromLat, 5)},{Math.Round(fromLng, 5)}:{Math.Round(toLat, 5)},{Math.Round(toLng, 5)}")
            : null;
        if (cacheKey is not null && _cache.TryGetValue(cacheKey, out RouteData? cached))
            return cached;

        try
        {
            var body = new
            {
                coordinates = new[] { new[] { fromLng, fromLat }, new[] { toLng, toLat } },
                extra_info = new[] { "waycategory" },
                options = new { avoid_features = new[] { "ferries" } },
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"v2/directions/{profile}/geojson")
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("Authorization", _apiKey);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/geo+json"));
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("ORS directions request failed with status {Status}: {Body}", response.StatusCode, errorBody);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var feature = doc.RootElement.GetProperty("features")[0];
            var properties = feature.GetProperty("properties");
            var summary = properties.GetProperty("summary");
            // ORS omits distance/duration when they are 0 (origin == destination)
            var distanceM = summary.TryGetProperty("distance", out var d) ? d.GetDouble() : 0;
            var durationS = summary.TryGetProperty("duration", out var t) ? t.GetDouble() : 0;

            // GeoJSON order is [lng, lat]; we return [lat, lng]
            var points = feature.GetProperty("geometry").GetProperty("coordinates").EnumerateArray()
                .Select(c => new[] { c[1].GetDouble(), c[0].GetDouble() })
                .ToList();

            var route = new RouteData(distanceM / 1000, durationS / 60, Downsample(points, MaxPoints), MotorwayShare(properties));
            if (cacheKey is not null) _cache.Set(cacheKey, route, WalkingCacheDuration);
            return route;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ORS directions request threw an exception");
            return null;
        }
    }

    /// <summary>Share of the route on highways, from the waycategory summary. Defaults to 0 if missing.</summary>
    private static double MotorwayShare(JsonElement properties)
    {
        if (!properties.TryGetProperty("extras", out var extras)
            || !extras.TryGetProperty("waycategory", out var wayCategory)
            || !wayCategory.TryGetProperty("summary", out var summary)
            || summary.ValueKind != JsonValueKind.Array)
            return 0;

        double highway = 0, total = 0;
        foreach (var item in summary.EnumerateArray())
        {
            // ORS may send the category as 1 or 1.0
            if (!item.TryGetProperty("value", out var v) || !v.TryGetDouble(out var raw)) continue;
            var value = (int)raw;
            // "amount" is the % of the route in this category
            var amount = item.TryGetProperty("amount", out var a) && a.TryGetDouble(out var x) ? x : 0;
            total += amount;
            if ((value & HighwayBit) != 0) highway += amount;
        }

        return total > 0 ? Math.Clamp(highway / total, 0, 1) : 0;
    }

    /// <summary>Keeps at most <paramref name="max"/> evenly spaced points, always including the first and last.</summary>
    private static List<double[]> Downsample(List<double[]> points, int max)
    {
        if (points.Count <= max) return points;

        var step = (points.Count - 1) / (double)(max - 1);
        return Enumerable.Range(0, max)
            .Select(i => points[(int)Math.Round(i * step)])
            .ToList();
    }
}
