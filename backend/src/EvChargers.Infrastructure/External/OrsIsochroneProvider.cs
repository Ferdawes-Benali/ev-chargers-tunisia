using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using EvChargers.Application.Interfaces;

namespace EvChargers.Infrastructure.External;

public class OrsIsochroneProvider : IOrsIsochroneProvider
{
    private readonly HttpClient _http;
    private readonly ILogger<OrsIsochroneProvider> _logger;
    private readonly string? _apiKey;

    public OrsIsochroneProvider(HttpClient http, IConfiguration config, ILogger<OrsIsochroneProvider> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = config["OrsApiKey"];
        _http.BaseAddress = new Uri("https://api.openrouteservice.org/");
    }

    public async Task<string?> GetIsochroneGeoJsonAsync(double lat, double lng, double rangeMeters, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("ORS API key not configured; skipping isochrone.");
            return null;
        }

        try
        {   
            var cappedMeters = Math.Min(rangeMeters, 120_000);
            var body = new
            {
                locations = new[] { new[] { lng, lat } },
                range = new[] { cappedMeters },
                range_type = "distance",
                options = new { avoid_features = new[] { "ferries" } },
            };

            var json = JsonSerializer.Serialize(body);

            using var request = new HttpRequestMessage(HttpMethod.Post, "v2/isochrones/driving-car")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("Authorization", _apiKey);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/geo+json"));
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("ORS isochrone request failed with status {Status}: {Body}", response.StatusCode, errorBody);
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ORS isochrone request threw an exception");
            return null;
        }
    }
}