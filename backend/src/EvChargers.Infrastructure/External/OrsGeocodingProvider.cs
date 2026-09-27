using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;

namespace EvChargers.Infrastructure.External;

public class OrsGeocodingProvider : IGeocodingProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OrsGeocodingProvider> _logger;
    private readonly string? _apiKey;

    public OrsGeocodingProvider(HttpClient http, IMemoryCache cache, IConfiguration config, ILogger<OrsGeocodingProvider> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        _apiKey = config["OrsApiKey"];
        _http.BaseAddress = new Uri("https://api.openrouteservice.org/");
    }

    public async Task<List<PlaceSuggestionDto>?> AutocompleteAsync(string query, double? focusLat, double? focusLng, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("ORS API key not configured; skipping geocoding.");
            return null;
        }

        var text = query.Trim();
        var hasFocus = focusLat.HasValue && focusLng.HasValue;

        // Rounded focus keeps nearby users on the same cache entry (protects the free ORS quota)
        var cacheKey = FormattableString.Invariant(
            $"geocode:{text.ToLowerInvariant()}:{(hasFocus ? $"{Math.Round(focusLat!.Value, 2)},{Math.Round(focusLng!.Value, 2)}" : "-")}");
        if (_cache.TryGetValue(cacheKey, out List<PlaceSuggestionDto>? cached))
            return cached;

        try
        {
            var url = $"geocode/autocomplete?text={Uri.EscapeDataString(text)}&boundary.country=TN&size=6";
            if (hasFocus)
                url += FormattableString.Invariant($"&focus.point.lat={focusLat!.Value}&focus.point.lon={focusLng!.Value}");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", _apiKey);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("ORS geocoding request failed with status {Status}: {Body}", response.StatusCode, errorBody);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var suggestions = new List<PlaceSuggestionDto>();
            foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
            {
                // GeoJSON order is [lng, lat]
                var coords = feature.GetProperty("geometry").GetProperty("coordinates");
                var label = feature.GetProperty("properties").TryGetProperty("label", out var l) ? l.GetString() : null;
                if (string.IsNullOrWhiteSpace(label) || coords.GetArrayLength() < 2) continue;

                suggestions.Add(new PlaceSuggestionDto(label, coords[1].GetDouble(), coords[0].GetDouble()));
            }

            _cache.Set(cacheKey, suggestions, CacheDuration);
            return suggestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ORS geocoding request threw an exception");
            return null;
        }
    }
}
