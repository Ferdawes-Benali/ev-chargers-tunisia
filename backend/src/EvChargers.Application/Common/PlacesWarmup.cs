using EvChargers.Application.DTOs;

namespace EvChargers.Application.Common;

/// <summary>Which stations the background warmup refreshes, and how often.</summary>
public static class PlacesWarmup
{
    /// <summary>Stored places older than this are refreshed.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);
    /// <summary>A valid but empty answer is rechecked sooner: it may have been a quiet server-side problem.</summary>
    public static readonly TimeSpan EmptyStaleAfter = TimeSpan.FromHours(6);
    /// <summary>Wait after the first failure; it doubles with each further failure in a row (see <see cref="RetryDelay"/>).</summary>
    public static readonly TimeSpan RetryFailedAfter = TimeSpan.FromMinutes(30);
    /// <summary>The retry wait never grows beyond this.</summary>
    public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromHours(24);
    public const int MaxPerRun = 20;
    /// <summary>A claim older than this is considered abandoned (a full server chain takes about 2 min).</summary>
    public static readonly TimeSpan RefreshClaimExpiresAfter = TimeSpan.FromMinutes(5);
    /// <summary>A station attempted more recently than this is not fetched again (duplicate sweep and queue requests).</summary>
    public static readonly TimeSpan SkipIfAttemptedWithin = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Wait before retrying after <paramref name="failures"/> failed attempts in a row: 30 min, 1 h, 2 h, 4 h… capped at 24 h.
    /// Fewer than one failure counts as one (entries that failed before the count existed).
    /// </summary>
    public static TimeSpan RetryDelay(int failures)
    {
        var doublings = Math.Clamp(failures, 1, 32) - 1;
        var minutes = RetryFailedAfter.TotalMinutes * Math.Pow(2, doublings);
        return minutes >= MaxRetryDelay.TotalMinutes ? MaxRetryDelay : TimeSpan.FromMinutes(minutes);
    }

    /// <summary>
    /// Due verified stations, the least recently attempted first (never attempted before all others), so a station that
    /// keeps failing waits its turn behind the others. Skipped: fresh places, and failures still inside their retry delay.
    /// At most <paramref name="max"/>.
    /// </summary>
    public static List<Guid> Select(IEnumerable<PlacesWarmupCandidate> candidates, DateTime now, int max = MaxPerRun) =>
        candidates
            .Where(c => c.Verified && IsDue(c, now))
            .OrderBy(c => c.LastAttemptAt ?? DateTime.MinValue)
            .Take(max)
            .Select(c => c.StationId)
            .ToList();

    public static bool IsDue(PlacesWarmupCandidate c, DateTime now)
    {
        if (!c.HasEntry) return true;
        var dataDue = c.FetchedAt is null || now - c.FetchedAt.Value >= (c.NoPlaces ? EmptyStaleAfter : StaleAfter);
        if (!dataDue) return false;
        return !c.LastAttemptFailed || c.LastAttemptAt is null || now - c.LastAttemptAt.Value >= RetryDelay(c.AttemptCount);
    }
}
