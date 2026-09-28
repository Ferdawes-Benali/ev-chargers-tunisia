using System.Security.Claims;
using System.Text.Json;

namespace EvChargers.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The Supabase user id of an authenticated request.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? throw new InvalidOperationException("No user id claim found"));

    /// <summary>The user's email, or null. The JWT handler may have renamed "email" to ClaimTypes.Email.</summary>
    public static string? GetEmail(this ClaimsPrincipal user) =>
        NullIfBlank(user.FindFirstValue("email") ?? user.FindFirstValue(ClaimTypes.Email));

    /// <summary>
    /// Name and avatar from Supabase's "user_metadata" JSON claim (filled by Google/GitHub).
    /// Name: full_name, then name, then the part of the email before "@". Never throws.
    /// </summary>
    public static (string? DisplayName, string? AvatarUrl) GetProfileFromMetadata(this ClaimsPrincipal user)
    {
        string? displayName = null, avatarUrl = null;

        var json = user.FindFirstValue("user_metadata");
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    displayName = GetString(doc.RootElement, "full_name") ?? GetString(doc.RootElement, "name");
                    avatarUrl = GetString(doc.RootElement, "avatar_url");
                }
            }
            catch (JsonException)
            {
                // Malformed metadata: fall through to the email-based name
            }
        }

        displayName ??= EmailLocalPart(user.GetEmail());
        return (displayName, avatarUrl);
    }

    private static string? GetString(JsonElement obj, string property) =>
        obj.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? NullIfBlank(value.GetString())
            : null;

    private static string? EmailLocalPart(string? email)
    {
        var at = email?.IndexOf('@') ?? -1;
        return at > 0 ? email![..at] : null;
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
