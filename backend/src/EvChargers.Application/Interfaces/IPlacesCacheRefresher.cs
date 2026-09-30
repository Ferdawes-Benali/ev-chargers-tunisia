namespace EvChargers.Application.Interfaces;

public interface IPlacesCacheRefresher
{
    /// <summary>
    /// Fetches the station's places from the provider and stores them. On failure the attempt and error are
    /// recorded and previous places are kept. Returns true on success; false on failure, unknown station, or when
    /// the station is already being (or was just) refreshed by another worker.
    /// </summary>
    Task<bool> RefreshAsync(Guid stationId, CancellationToken ct);
}
