namespace EvChargers.Application.DTOs;

/// <param name="Places">Null until a fetch has succeeded.</param>
/// <param name="LastError">Why the last attempt failed; null when it succeeded.</param>
/// <param name="AttemptCount">Failed attempts since the last success.</param>
public record PlacesCacheSnapshot(List<RawPlace>? Places, DateTime? FetchedAt, DateTime LastAttemptAt, string? LastError,
                                  int AttemptCount = 0);

/// <param name="HasEntry">False when no fetch was ever attempted for this station.</param>
/// <param name="NoPlaces">The last successful fetch found nothing nearby (a valid, empty answer).</param>
/// <param name="LastAttemptFailed">The last attempt failed (the entry has a LastError).</param>
/// <param name="AttemptCount">Failed attempts since the last success; sets the retry delay.</param>
public record PlacesWarmupCandidate(Guid StationId, bool Verified, bool HasEntry, DateTime? FetchedAt, DateTime? LastAttemptAt,
                                    bool NoPlaces = false, bool LastAttemptFailed = false, int AttemptCount = 0);
