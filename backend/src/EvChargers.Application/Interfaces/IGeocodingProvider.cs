using EvChargers.Application.DTOs;

namespace EvChargers.Application.Interfaces;

public interface IGeocodingProvider
{
    /// <summary>Place suggestions for a partial query, or null if the provider is unavailable.</summary>
    Task<List<PlaceSuggestionDto>?> AutocompleteAsync(string query, double? focusLat, double? focusLng, CancellationToken ct);
}
