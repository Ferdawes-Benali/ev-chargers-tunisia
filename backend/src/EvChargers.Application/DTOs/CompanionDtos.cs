namespace EvChargers.Application.DTOs;

/// <summary>A place's name in each app language, when OpenStreetMap has it.</summary>
public record PlaceNames(string? Fr, string? Ar, string? En)
{
    public static readonly PlaceNames None = new(null, null, null);
}

/// <summary>A point of interest as returned by the places provider, before any walking logic.</summary>
/// <param name="Id">Stable OSM id, e.g. "node/715619811".</param>
/// <param name="Name">Best available name (name, then name:fr, name:ar, else a generic label).</param>
/// <param name="IsNamed">False when <paramref name="Name"/> is only a generic label like "Café".</param>
/// <param name="OpeningHours">Raw OSM "opening_hours" tag, if any.</param>
public record RawPlace(string Id, string Name, bool IsNamed, string Category, double Lat, double Lng,
                       PlaceNames Names, string? OpeningHours = null);

/// <param name="OpenStatus">"open", "closed" or "unknown".</param>
/// <param name="InDefaultList">False for extra ATMs/banks: shown only under the "essentials" filter.</param>
public record CompanionPlaceDto(string Id, string Name, PlaceNames Names, string Category, string Group,
                                double Lat, double Lng, int DistanceMeters, int WalkMinutes, string Band,
                                string OpenStatus, string? ClosesAt, string? OpensAt, bool InDefaultList);

/// <param name="Kind">"coffee", "lunch", "dinner", "pray" or "walk" (see PickKinds); the frontend translates it.</param>
public record CompanionPickDto(string Kind, string PlaceId, int WalkMinutes);

/// <summary>Values of <see cref="CompanionResultDto.Status"/>.</summary>
public static class CompanionStatuses
{
    public const string Ready = "ready";
    /// <summary>No places stored yet; the background warmup has been asked to fetch them.</summary>
    public const string Preparing = "preparing";
    /// <summary>Every fetch so far failed and there are no places at all.</summary>
    public const string Unavailable = "unavailable";
}

/// <param name="BackBy">Local "HH:mm" when the car should be ready; null without a charge time.</param>
/// <param name="Status">"ready", "preparing" or "unavailable" (see <see cref="CompanionStatuses"/>).</param>
/// <param name="FetchedAt">When the places were fetched from OpenStreetMap (UTC); null without places.</param>
public record CompanionResultDto(int? ChargeMinutes, int? MaxPowerKw, string? BackBy,
                                 List<CompanionPickDto> Picks, List<CompanionPlaceDto> Places,
                                 string Source, bool Unavailable,
                                 string Status = CompanionStatuses.Ready, DateTime? FetchedAt = null);

/// <param name="Points">Walking path as [lat, lng]; empty when <paramref name="Estimated"/>.</param>
/// <param name="Estimated">True when routing was unavailable and the numbers come from straight-line distance.</param>
public record WalkingRouteDto(int DistanceMeters, int DurationMinutes, List<double[]> Points, bool Estimated);
