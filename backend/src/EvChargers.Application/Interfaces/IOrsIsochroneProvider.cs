namespace EvChargers.Application.Interfaces;

public interface IOrsIsochroneProvider
{
    Task<string?> GetIsochroneGeoJsonAsync(double lat, double lng, double rangeMeters, CancellationToken ct);
}