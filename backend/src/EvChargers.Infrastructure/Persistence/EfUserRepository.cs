using Microsoft.EntityFrameworkCore;
using EvChargers.Application.Interfaces;
using EvChargers.Domain.Entities;

namespace EvChargers.Infrastructure.Persistence;

public class EfUserRepository : IUserRepository
{
    private readonly AppDbContext _db;
    public EfUserRepository(AppDbContext db) => _db = db;

    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.AppUsers.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task UpsertAsync(AppUser user, CancellationToken ct)
    {
        var existing = await _db.AppUsers.FindAsync([user.Id], ct);
        if (existing is null)
        {
            _db.AppUsers.Add(user);
        }
        else
        {
            existing.DisplayName = user.DisplayName;
            existing.AvatarUrl = user.AvatarUrl;
        }
        await _db.SaveChangesAsync(ct);
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
}