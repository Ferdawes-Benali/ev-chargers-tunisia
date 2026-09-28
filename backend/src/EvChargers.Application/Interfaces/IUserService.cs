using EvChargers.Application.DTOs;

namespace EvChargers.Application.Interfaces;

public interface IUserService
{
    /// <summary>
    /// Creates the user on first login (and queues the welcome email), or refreshes
    /// Email/DisplayName/AvatarUrl of an existing user. IsAdmin, favorites and the stored
    /// language of an existing user are never changed here.
    /// </summary>
    Task<(UserProfileDto Profile, bool IsNew)> EnsureUserAsync(
        Guid userId, string? email, string? displayName, string? avatarUrl, string preferredLanguage, CancellationToken ct);

    /// <summary>Returns false when the user does not exist. The language must already be validated.</summary>
    Task<bool> SetLanguageAsync(Guid userId, string language, CancellationToken ct);
}
