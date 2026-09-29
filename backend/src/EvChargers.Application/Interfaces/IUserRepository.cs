using EvChargers.Domain.Entities;

namespace EvChargers.Application.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct);
    /// <summary>Display names of the given users in one query. Unknown ids are missing from the result.</summary>
    Task<IReadOnlyDictionary<Guid, string?>> GetDisplayNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    /// <summary>
    /// Inserts the user, or updates Email/DisplayName/AvatarUrl of the existing row.
    /// Returns true only when a new row was inserted; false when the row already existed,
    /// including when a concurrent request inserted the same id first.
    /// </summary>
    Task<bool> UpsertAsync(AppUser user, CancellationToken ct);
    /// <summary>Returns false when the user does not exist.</summary>
    Task<bool> SetLanguageAsync(Guid userId, string language, CancellationToken ct);
    Task AddFavoriteAsync(Guid userId, Guid stationId, CancellationToken ct);
    Task RemoveFavoriteAsync(Guid userId, Guid stationId, CancellationToken ct);
    /// <summary>Removes several favorites at once (e.g. stations that no longer exist).</summary>
    Task RemoveFavoritesAsync(Guid userId, IReadOnlyCollection<Guid> stationIds, CancellationToken ct);
}
