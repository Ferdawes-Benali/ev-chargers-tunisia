using EvChargers.Application.DTOs;

namespace EvChargers.Application.Interfaces;

/// <summary>Stored nearby places per station (see <see cref="Domain.Entities.PlacesCacheEntry"/>).</summary>
public interface IPlacesCacheRepository
{
    /// <summary>The station's entry, or null if nothing was ever attempted.</summary>
    Task<PlacesCacheSnapshot?> GetAsync(Guid stationId, CancellationToken ct);

    /// <summary>Every station with its cache state, for the warmup to choose from (see <see cref="Common.PlacesWarmup"/>).</summary>
    Task<List<PlacesWarmupCandidate>> GetWarmupCandidatesAsync(CancellationToken ct);

    /// <summary>
    /// Atomically claims the station for a fetch (creating its entry if needed). False when another worker holds an
    /// unexpired claim or an attempt finished within <see cref="Common.PlacesWarmup.SkipIfAttemptedWithin"/>.
    /// Saving a success or failure releases the claim.
    /// </summary>
    Task<bool> TryStartRefreshAsync(Guid stationId, DateTime now, CancellationToken ct);

    Task SaveSuccessAsync(Guid stationId, List<RawPlace> places, DateTime at, CancellationToken ct);

    /// <summary>Records a failed attempt; previously stored places are kept.</summary>
    Task SaveFailureAsync(Guid stationId, string error, DateTime at, CancellationToken ct);
}
