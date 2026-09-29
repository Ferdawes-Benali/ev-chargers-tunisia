namespace EvChargers.Application.DTOs;

/// <param name="PreferredLanguage">"fr", "ar" or "en": the frontend applies it after login when the browser has no saved choice.</param>
public record UserProfileDto(Guid Id, string? DisplayName, string? AvatarUrl, bool IsAdmin, List<Guid> FavoriteStationIds,
                             string PreferredLanguage);

public record UpdateLanguageRequest(string? Language);
