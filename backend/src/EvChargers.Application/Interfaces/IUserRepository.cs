using EvChargers.Domain.Entities;

namespace EvChargers.Application.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct);
    Task UpsertAsync(AppUser user, CancellationToken ct);
    Task AddFavoriteAsync(Guid userId, Guid stationId, CancellationToken ct);
    Task RemoveFavoriteAsync(Guid userId, Guid stationId, CancellationToken ct);
}