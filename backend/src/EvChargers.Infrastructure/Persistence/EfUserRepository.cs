using Microsoft.EntityFrameworkCore;
using Npgsql;
using EvChargers.Application.Interfaces;
using EvChargers.Domain.Entities;

namespace EvChargers.Infrastructure.Persistence;

public class EfUserRepository : IUserRepository
{
    private readonly AppDbContext _db;
    public EfUserRepository(AppDbContext db) => _db = db;

    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyDictionary<Guid, string?>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string?>();
        return await _db.AppUsers
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }

    public async Task<bool> UpsertAsync(AppUser user, CancellationToken ct)
    {
        var existing = await _db.AppUsers.FindAsync([user.Id], ct);
        if (existing is not null)
        {
            existing.Email = user.Email;
            existing.DisplayName = user.DisplayName;
            existing.AvatarUrl = user.AvatarUrl;
            await _db.SaveChangesAsync(ct);
            return false;
        }

        _db.AppUsers.Add(user);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another request (e.g. two tabs on first login) inserted this id first.
            // Stop tracking our copy so the caller can reload the stored row.
            _db.Entry(user).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<bool> SetLanguageAsync(Guid userId, string language, CancellationToken ct)
    {
        var user = await _db.AppUsers.FindAsync([userId], ct);
        if (user is null) return false;
        if (user.PreferredLanguage != language)
        {
            user.PreferredLanguage = language;
            await _db.SaveChangesAsync(ct);
        }
        return true;
    }

    public async Task AddFavoriteAsync(Guid userId, Guid stationId, CancellationToken ct)
    {
        var user = await _db.AppUsers.FindAsync([userId], ct);
        if (user is null) return;
        if (!user.FavoriteStationIds.Contains(stationId))
        {
            user.FavoriteStationIds.Add(stationId);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RemoveFavoriteAsync(Guid userId, Guid stationId, CancellationToken ct)
    {
        var user = await _db.AppUsers.FindAsync([userId], ct);
        if (user is null) return;
        if (user.FavoriteStationIds.Remove(stationId))
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RemoveFavoritesAsync(Guid userId, IReadOnlyCollection<Guid> stationIds, CancellationToken ct)
    {
        if (stationIds.Count == 0) return;
        var user = await _db.AppUsers.FindAsync([userId], ct);
        if (user is null) return;
        if (user.FavoriteStationIds.RemoveAll(stationIds.Contains) > 0)
        {
            await _db.SaveChangesAsync(ct);
        }
    }
}
