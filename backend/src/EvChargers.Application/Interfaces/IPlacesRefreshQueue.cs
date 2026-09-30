namespace EvChargers.Application.Interfaces;

/// <summary>Asks the background warmup to fetch one station's places soon.</summary>
public interface IPlacesRefreshQueue
{
    /// <summary>Queues the station; false if it is already waiting (never blocks).</summary>
    bool Request(Guid stationId);
}
