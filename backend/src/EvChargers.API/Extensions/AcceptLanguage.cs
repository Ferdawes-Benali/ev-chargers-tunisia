using Microsoft.Net.Http.Headers;
using EvChargers.Application.Common;

namespace EvChargers.API.Extensions;

public static class AcceptLanguage
{
    /// <summary>
    /// The preferred supported language ("fr", "ar" or "en") from an Accept-Language header,
    /// by quality then order: "de-DE,ar-TN;q=0.8" → "ar". French when nothing matches.
    /// </summary>
    public static string PickSupported(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)
            || !StringWithQualityHeaderValue.TryParseList([header], out var entries))
            return Languages.Default;

        var match = entries
            .Select((e, index) => (Code: e.Value.Value?.Split('-')[0].ToLowerInvariant(), Quality: e.Quality ?? 1.0, index))
            .Where(e => e.Quality > 0 && Languages.IsSupported(e.Code))
            .OrderByDescending(e => e.Quality)
            .ThenBy(e => e.index)
            .FirstOrDefault();

        return match.Code ?? Languages.Default;
    }
}
