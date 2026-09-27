using System.Security.Claims;

namespace EvChargers.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The Supabase user id of an authenticated request.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? throw new InvalidOperationException("No user id claim found"));
}
