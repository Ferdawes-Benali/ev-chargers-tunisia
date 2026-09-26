namespace EvChargers.Application.DTOs;

public record UserProfileDto(Guid Id, string? DisplayName, string? AvatarUrl, bool IsAdmin, List<Guid> FavoriteStationIds);