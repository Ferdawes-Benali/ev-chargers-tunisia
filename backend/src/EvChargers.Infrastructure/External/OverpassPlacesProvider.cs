using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using EvChargers.Application.Common;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;

namespace EvChargers.Infrastructure.External;

/// <summary>
/// Nearby places from OpenStreetMap through the public Overpass API.
/// The shared server is often busy, so: one retry on 429/504, then a mirror, then yesterday's copy.
/// </summary>
public partial class OverpassPlacesProvider : IPlacesProvider
{
    public const string MainUrl = "https://overpass-api.de/api/interpreter";
    public const string MirrorUrl = "https://overpass.kumi.systems/api/interpreter";

    // POIs change slowly: refresh daily, but a week-old list beats "unavailable"
    public static readonly TimeSpan FreshFor = TimeSpan.FromHours(24);
    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(7);

    private const double DuplicateMeters = 20;
    private const int MaxLoggedBodyLength = 500;

    private static readonly Dictionary<string, string> GenericNames = new()
    {
        [PlaceCategories.Cafe] = "Café",
        [PlaceCategories.Restaurant] = "Restaurant",
        [PlaceCategories.Mosque] = "Mosque",
        [PlaceCategories.Park] = "Park",
        [PlaceCategories.Shopping] = "Shop",
        [PlaceCategories.Pharmacy] = "Pharmacy",
        [PlaceCategories.Toilets] = "Toilets",
        [PlaceCategories.Atm] = "ATM",
    };

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OverpassPlacesProvider> _logger;

    /// <summary>Delay before retrying a busy (429/504) server.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1.5);
    /// <summary>Decides when a cached copy is stale. Settable for tests.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    private sealed record CachedPlaces(List<RawPlace> Places, DateTimeOffset FetchedAt);

    public OverpassPlacesProvider(HttpClient http, IMemoryCache cache, ILogger<OverpassPlacesProvider> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(20);
        // Overpass usage policy: identify the application
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("EVChargersTunisia/1.0 (internship project)");
    }

    public async Task<List<RawPlace>?> GetNearbyAsync(double lat, double lng, int radiusMeters, CancellationToken ct)
    {
        var cacheKey = FormattableString.Invariant($"places:{Math.Round(lat, 4)},{Math.Round(lng, 4)}:{radiusMeters}");
        _cache.TryGetValue(cacheKey, out CachedPlaces? cached);
        var now = Clock.GetUtcNow();
        if (cached is not null && now - cached.FetchedAt < FreshFor)
            return cached.Places;

        var fresh = await FetchAsync(BuildQuery(lat, lng, radiusMeters), ct);
        if (fresh is not null)
        {
            _cache.Set(cacheKey, new CachedPlaces(fresh, now), KeepFor);
            return fresh;
        }

        if (cached is not null)
        {
            _logger.LogWarning("Overpass unavailable; serving places cached at {FetchedAt:u}", cached.FetchedAt);
            return cached.Places;
        }
        return null;
    }

    /// <summary>Queries the main server, retries once if busy, and then tries the mirror. Returns null if all requests fail.</summary>
    private async Task<List<RawPlace>?> FetchAsync(string query, CancellationToken ct)
    {
        try
        {
            var (places, status) = await TryServerAsync(MainUrl, query, ct);
            if (places is not null) return places;

            if (status is HttpStatusCode.TooManyRequests or HttpStatusCode.GatewayTimeout)
            {
                await Task.Delay(RetryDelay, ct);
                (places, _) = await TryServerAsync(MainUrl, query, ct);
                if (places is not null) return places;
            }

            ct.ThrowIfCancellationRequested();
            (places, _) = await TryServerAsync(MirrorUrl, query, ct);
            return places;
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            return null; // the caller went away
        }
    }

    /// <summary>One request. Returns the places, or null plus the HTTP status (null status = network error/timeout).</summary>
    private async Task<(List<RawPlace>? Places, HttpStatusCode? Status)> TryServerAsync(string url, string query, CancellationToken ct)
    {
        var server = new Uri(url).Host;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent([new("data", query)]),
            };
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                if (errorBody.Length > MaxLoggedBodyLength) errorBody = errorBody[..MaxLoggedBodyLength] + "…";
                _logger.LogWarning("Overpass request to {Server} failed with status {Status}: {Body}", server, response.StatusCode, errorBody);
                return (null, response.StatusCode);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return (Parse(doc.RootElement), response.StatusCode);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Includes the 20 s HttpClient timeout (TaskCanceledException without our token cancelled)
            _logger.LogWarning(ex, "Overpass request to {Server} failed or timed out", server);
            return (null, null);
        }
    }

    /// <summary>
    /// One query for every category. "nwr" = nodes, ways and relations; "out center" gives
    /// ways/relations (buildings, parks) a single center point.
    /// </summary>
    public static string BuildQuery(double lat, double lng, int radiusMeters)
    {
        var around = FormattableString.Invariant($"around:{radiusMeters},{lat},{lng}");
        return $"""
            [out:json][timeout:15];
            (
              nwr({around})["amenity"~"^(cafe|restaurant|fast_food|pharmacy|toilets|atm|bank)$"];
              nwr({around})["amenity"="place_of_worship"]["religion"="muslim"];
              nwr({around})["leisure"~"^(park|garden)$"];
              nwr({around})["shop"~"^(supermarket|mall|convenience)$"];
            );
            out center tags;
            """;
    }

    /// <summary>Turns an Overpass JSON response into places. Elements without a position or a known category are skipped.</summary>
    public static List<RawPlace> Parse(JsonElement root)
    {
        var places = new List<RawPlace>();
        if (!root.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
            return places;

        var unnamedUtilities = new List<RawPlace>();
        foreach (var element in elements.EnumerateArray())
        {
            if (!element.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Object) continue;
            if (!TryGetPosition(element, out var lat, out var lng)) continue;

            var category = Categorize(tags);
            if (category is null) continue;

            // Stable across requests: OSM type + id, e.g. "node/715619811"
            var id = element.TryGetProperty("type", out var type) && element.TryGetProperty("id", out var osmId)
                ? $"{type.GetString()}/{osmId.GetRawText()}"
                : null;
            if (id is null) continue;
            var openingHours = Tag(tags, "opening_hours");

            var plainName = Tag(tags, "name");
            var names = new PlaceNames(
                Fr: Tag(tags, "name:fr"),
                Ar: Tag(tags, "name:ar") ?? (plainName is not null && IsArabicScript(plainName) ? plainName : null),
                En: Tag(tags, "name:en"));
            var name = plainName ?? names.Fr ?? names.Ar;

            if (name is null && category is PlaceCategories.Toilets or PlaceCategories.Atm)
            {
                // Several unnamed ATMs/toilets mapped at the same spot are one stop for the driver
                if (unnamedUtilities.Any(p => p.Category == category
                        && GeoUtils.HaversineDistanceKm(p.Lat, p.Lng, lat, lng) * 1000 < DuplicateMeters))
                    continue;
                var place = new RawPlace(id, GenericNames[category], IsNamed: false, category, lat, lng, names, openingHours);
                unnamedUtilities.Add(place);
                places.Add(place);
                continue;
            }

            places.Add(new RawPlace(id, name ?? GenericNames[category], IsNamed: name is not null, category, lat, lng, names, openingHours));
        }
        return places;
    }

    /// <summary>Arabic letters and no Latin ones ("جامع الزيتونة" yes, "Café الياسمين" no).</summary>
    private static bool IsArabicScript(string text) => ArabicLetter().IsMatch(text) && !LatinLetter().IsMatch(text);

    [GeneratedRegex(@"\p{IsArabic}")]
    private static partial Regex ArabicLetter();

    // Basic Latin plus accented letters (é, ç…), U+00C0–U+024F
    [GeneratedRegex(@"[A-Za-zÀ-ɏ]")]
    private static partial Regex LatinLetter();

    private static string? Categorize(JsonElement tags)
    {
        switch (Tag(tags, "amenity"))
        {
            case "cafe": return PlaceCategories.Cafe;
            case "restaurant" or "fast_food": return PlaceCategories.Restaurant;
            case "place_of_worship" when Tag(tags, "religion") == "muslim": return PlaceCategories.Mosque;
            case "pharmacy": return PlaceCategories.Pharmacy;
            case "toilets": return PlaceCategories.Toilets;
            case "atm" or "bank": return PlaceCategories.Atm;
        }
        if (Tag(tags, "leisure") is "park" or "garden") return PlaceCategories.Park;
        if (Tag(tags, "shop") is "supermarket" or "mall" or "convenience") return PlaceCategories.Shopping;
        return null;
    }

    /// <summary>Nodes carry lat/lon; ways and relations carry a "center" object.</summary>
    private static bool TryGetPosition(JsonElement element, out double lat, out double lng)
    {
        var source = element.TryGetProperty("center", out var center) ? center : element;
        lat = lng = 0;
        return source.TryGetProperty("lat", out var latEl) && latEl.TryGetDouble(out lat)
            && source.TryGetProperty("lon", out var lngEl) && lngEl.TryGetDouble(out lng);
    }

    private static string? Tag(JsonElement tags, string key) =>
        tags.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;
}
