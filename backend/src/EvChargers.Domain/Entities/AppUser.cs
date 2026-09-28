namespace EvChargers.Domain.Entities;

public class AppUser
{
    public Guid Id { get; set; } // matches Supabase auth user id
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string PreferredLanguage { get; set; } = "fr"; // "fr" | "ar" | "en"
    public bool IsAdmin { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Guid> FavoriteStationIds { get; set; } = [];
}
