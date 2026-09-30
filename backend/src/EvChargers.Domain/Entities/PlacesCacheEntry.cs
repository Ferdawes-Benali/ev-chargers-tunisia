namespace EvChargers.Domain.Entities;

/// <summary>
/// Nearby places for one station, fetched in the background so requests never wait on Overpass.
/// A failed refresh keeps the previous places and only records the attempt.
/// </summary>
public class PlacesCacheEntry
{
    public const int MaxErrorLength = 300;

    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    /// <summary>The parsed place list as JSON; null until a fetch has succeeded.</summary>
    public string? PlacesJson { get; set; }
    /// <summary>When <see cref="PlacesJson"/> was fetched; null until a fetch has succeeded.</summary>
    public DateTime? FetchedAt { get; set; }
    public DateTime LastAttemptAt { get; set; }
    /// <summary>Why the last attempt failed; null when it succeeded.</summary>
    public string? LastError { get; set; }
    /// <summary>Failed attempts since the last success (0 after a success).</summary>
    public int AttemptCount { get; set; }
    /// <summary>
    /// Set when a worker claims the station for a fetch, cleared when the attempt is recorded. Stops two workers
    /// (e.g. two API instances on one database) fetching the same station at once; an abandoned claim expires.
    /// </summary>
    public DateTime? RefreshStartedAt { get; set; }

    public void RecordSuccess(string placesJson, DateTime at)
    {
        PlacesJson = placesJson;
        FetchedAt = at;
        LastAttemptAt = at;
        LastError = null;
        AttemptCount = 0;
        RefreshStartedAt = null;
    }

    /// <summary>Records the failed attempt; the previous places, if any, stay.</summary>
    public void RecordFailure(string error, DateTime at)
    {
        LastAttemptAt = at;
        LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
        AttemptCount++;
        RefreshStartedAt = null;
    }
}
