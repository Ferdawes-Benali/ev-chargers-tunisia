using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using EvChargers.Application.Common;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;
using EvChargers.Domain.Entities;
using EvChargers.Domain.Enums;

namespace EvChargers.Infrastructure.Persistence;

public class EfPlacesCacheRepository : IPlacesCacheRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    /// <summary>How an empty place list is stored (a valid answer with nothing nearby).</summary>
    private const string NoPlacesJson = "[]";

    private readonly AppDbContext _db;
    public EfPlacesCacheRepository(AppDbContext db) => _db = db;

    public async Task<PlacesCacheSnapshot?> GetAsync(Guid stationId, CancellationToken ct)
    {
        var entry = await _db.PlacesCache.AsNoTracking().FirstOrDefaultAsync(p => p.StationId == stationId, ct);
        if (entry is null) return null;

        var places = entry.PlacesJson is null ? null : JsonSerializer.Deserialize<List<RawPlace>>(entry.PlacesJson, Json);
        return new PlacesCacheSnapshot(places, entry.FetchedAt, entry.LastAttemptAt, entry.LastError, entry.AttemptCount);
    }

    public async Task<List<PlacesWarmupCandidate>> GetWarmupCandidatesAsync(CancellationToken ct)
    {
        var entries = await _db.PlacesCache.AsNoTracking()
            .Select(p => new
            {
                p.StationId,
                p.FetchedAt,
                p.LastAttemptAt,
                NoPlaces = p.PlacesJson == NoPlacesJson,
                Failed = p.LastError != null,
                p.AttemptCount,
            })
            .ToDictionaryAsync(p => p.StationId, ct);
        var stations = await _db.Stations.AsNoTracking()
            .Select(s => new { s.Id, s.Status })
            .ToListAsync(ct);

        return stations.Select(s => entries.TryGetValue(s.Id, out var e)
                ? new PlacesWarmupCandidate(s.Id, s.Status == StationStatus.Verified, true, e.FetchedAt, e.LastAttemptAt,
                                            e.NoPlaces, e.Failed, e.AttemptCount)
                : new PlacesWarmupCandidate(s.Id, s.Status == StationStatus.Verified, false, null, null))
            .ToList();
    }

    public async Task<bool> TryStartRefreshAsync(Guid stationId, DateTime now, CancellationToken ct)
    {
        // Create the row first if missing; ON CONFLICT makes two first attempts safe. Neither FetchedAt nor
        // LastError is set, so it counts as "not attempted yet" below.
        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO ""PlacesCache"" (""Id"", ""StationId"", ""LastAttemptAt"", ""AttemptCount"")
            VALUES ({Guid.NewGuid()}, {stationId}, {now}, 0)
            ON CONFLICT (""StationId"") DO NOTHING", ct);

        // One UPDATE decides the winner: Postgres re-checks the WHERE on the locked row
        var claimExpired = now - PlacesWarmup.RefreshClaimExpiresAfter;
        var attemptedAfter = now - PlacesWarmup.SkipIfAttemptedWithin;
        var claimed = await _db.PlacesCache
            .Where(p => p.StationId == stationId
                        && (p.RefreshStartedAt == null || p.RefreshStartedAt < claimExpired)
                        && !((p.FetchedAt != null || p.LastError != null) && p.LastAttemptAt > attemptedAfter))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.RefreshStartedAt, now), ct);
        return claimed == 1;
    }

    public async Task SaveSuccessAsync(Guid stationId, List<RawPlace> places, DateTime at, CancellationToken ct)
    {
        var entry = await GetOrAddAsync(stationId, ct);
        entry.RecordSuccess(JsonSerializer.Serialize(places, Json), at);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveFailureAsync(Guid stationId, string error, DateTime at, CancellationToken ct)
    {
        var entry = await GetOrAddAsync(stationId, ct);
        entry.RecordFailure(error, at);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<PlacesCacheEntry> GetOrAddAsync(Guid stationId, CancellationToken ct)
    {
        var entry = await _db.PlacesCache.FirstOrDefaultAsync(p => p.StationId == stationId, ct);
        if (entry is not null) return entry;

        entry = new PlacesCacheEntry { Id = Guid.NewGuid(), StationId = stationId };
        _db.PlacesCache.Add(entry);
        return entry;
    }
}
