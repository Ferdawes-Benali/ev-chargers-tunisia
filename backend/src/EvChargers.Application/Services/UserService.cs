using Microsoft.Extensions.Logging;
using EvChargers.Application.Common;
using EvChargers.Application.DTOs;
using EvChargers.Application.Email;
using EvChargers.Application.Interfaces;
using EvChargers.Domain.Entities;

namespace EvChargers.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IEmailQueue _emailQueue;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository users, IEmailQueue emailQueue, ILogger<UserService> logger)
    {
        _users = users;
        _emailQueue = emailQueue;
        _logger = logger;
    }

    public async Task<(UserProfileDto Profile, bool IsNew)> EnsureUserAsync(
        Guid userId, string? email, string? displayName, string? avatarUrl, string preferredLanguage, CancellationToken ct)
    {
        email = NullIfBlank(email);
        displayName = NullIfBlank(displayName);
        avatarUrl = NullIfBlank(avatarUrl);

        var existing = await _users.GetByIdAsync(userId, ct);
        if (existing is not null)
        {
            // Missing claims never erase what we already know
            var changed = false;
            if (email is not null && email != existing.Email) { existing.Email = email; changed = true; }
            if (displayName is not null && displayName != existing.DisplayName) { existing.DisplayName = displayName; changed = true; }
            if (avatarUrl is not null && avatarUrl != existing.AvatarUrl) { existing.AvatarUrl = avatarUrl; changed = true; }
            if (changed) await _users.UpsertAsync(existing, ct);
            return (ToDto(existing), false);
        }

        var user = new AppUser
        {
            Id = userId,
            Email = email,
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
            PreferredLanguage = Languages.Normalize(preferredLanguage),
        };

        if (!await _users.UpsertAsync(user, ct))
        {
            // Lost the first-login race: the other request created the row (and sends the welcome email)
            var stored = await _users.GetByIdAsync(userId, ct) ?? user;
            return (ToDto(stored), false);
        }

        if (user.Email is not null)
            await QueueWelcomeAsync(user, ct);

        return (ToDto(user), true);
    }

    public Task<bool> SetLanguageAsync(Guid userId, string language, CancellationToken ct) =>
        _users.SetLanguageAsync(userId, language, ct);

    private async Task QueueWelcomeAsync(AppUser user, CancellationToken ct)
    {
        try
        {
            await _emailQueue.EnqueueAsync(EmailTemplates.Welcome(user.Email!, user.DisplayName, user.PreferredLanguage), ct);
        }
        catch (Exception ex)
        {
            // An email problem must never break login
            _logger.LogWarning(ex, "Could not queue the welcome email for user {UserId}", user.Id);
        }
    }

    private static UserProfileDto ToDto(AppUser u) =>
        new(u.Id, u.DisplayName, u.AvatarUrl, u.IsAdmin, u.FavoriteStationIds);

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
