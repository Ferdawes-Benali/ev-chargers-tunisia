using EvChargers.Application.DTOs;

namespace EvChargers.Application.Interfaces;

public interface IPlacesProvider
{
    /// <summary>
    /// Places around a point (categories in <see cref="Common.PlaceCategories"/>),
    /// or null if the provider is unavailable.
    /// </summary>
    Task<List<RawPlace>?> GetNearbyAsync(double lat, double lng, int radiusMeters, CancellationToken ct);
}
