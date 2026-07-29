using EvChargers.Application.DTOs;

namespace EvChargers.Application.Common;

/// <summary>Pure logic of the Charging Companion: which nearby places fit in the charging time, and in what order.</summary>
public static class Companion
{
    /// <summary>One-way walk used when we don't know how long charging takes.</summary>
    public const int DefaultOneWayMinutes = 15;
    public const int MaxPerCategory = 8;
    public const int MaxTotal = 40;
    /// <summary>ATMs and banks are everywhere in town centres: keep the default list diverse.</summary>
    public const int MaxAtmsInDefaultList = 2;
    public const int MinRadiusMeters = 300;
    public const int MaxRadiusMeters = 1500;
    public const int MaxPicks = 3;

    private static readonly TimeOnly LunchStart = new(11, 30), LunchEnd = new(14, 30);
    private static readonly TimeOnly DinnerStart = new(18, 30), DinnerEnd = new(22, 0);

    /// <summary>There and back while charging: half the charge time each way.</summary>
    public static int OneWayBudgetMinutes(int? chargeMinutes) =>
        chargeMinutes is { } minutes ? minutes / 2 : DefaultOneWayMinutes;

    public static int SearchRadiusMeters(int? chargeMinutes)
    {
        var radius = Walking.RadiusMeters(OneWayBudgetMinutes(chargeMinutes));
        return (int)Math.Clamp(Math.Round(radius), MinRadiusMeters, MaxRadiusMeters);
    }

    public static string Band(int walkMinutes) => walkMinutes switch
    {
        <= 2 => "≤2 min",
        <= 5 => "≤5 min",
        <= 10 => "≤10 min",
        <= 15 => "≤15 min",
        _ => "farther",
    };

    /// <summary>"HH:mm" local time when the car should be ready.</summary>
    public static string? BackBy(DateTime localNow, int? chargeMinutes) =>
        chargeMinutes is { } minutes ? localNow.AddMinutes(minutes).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) : null;

    /// <summary>
    /// Keeps places whose round trip fits the charge (15 min one way without a charge time), then ranks:
    /// named first, then open, unknown, closed (closed stay, last), then walking time.
    /// </summary>
    public static List<CompanionPlaceDto> Build(double stationLat, double stationLng, IEnumerable<RawPlace> places,
                                                int? chargeMinutes, DateTime localNow)
    {
        var ranked = places
            .Select(p =>
            {
                var meters = Walking.DistanceMeters(stationLat, stationLng, p.Lat, p.Lng);
                var walk = Walking.WalkMinutes(meters);
                var open = OpeningHours.Evaluate(p.OpeningHours, localNow);
                return (Raw: p, Dto: new CompanionPlaceDto(p.Id, p.Name, p.Names, p.Category, PlaceGroups.Of(p.Category),
                    p.Lat, p.Lng, meters, walk, Band(walk), open.Status, open.ClosesAt, open.OpensAt, InDefaultList: true));
            })
            .Where(x => chargeMinutes is { } charge
                ? 2 * x.Dto.WalkMinutes <= charge
                : x.Dto.WalkMinutes <= DefaultOneWayMinutes)
            .OrderBy(x => x.Raw.IsNamed ? 0 : 1)
            .ThenBy(x => StatusRank(x.Dto.OpenStatus))
            .ThenBy(x => x.Dto.WalkMinutes)
            .ThenBy(x => x.Dto.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Dto);

        var perCategory = new Dictionary<string, int>();
        var result = new List<CompanionPlaceDto>();
        foreach (var place in ranked)
        {
            var count = perCategory.GetValueOrDefault(place.Category);
            if (count >= MaxPerCategory) continue;
            perCategory[place.Category] = count + 1;

            var extraAtm = place.Category == PlaceCategories.Atm && count >= MaxAtmsInDefaultList;
            result.Add(extraAtm ? place with { InDefaultList = false } : place);
            if (result.Count == MaxTotal) break;
        }
        return result;
    }

    /// <summary>
    /// Up to 3 suggestions: a quick coffee, a meal at meal times, a mosque; a park fills any missing slot.
    /// Closed places are never suggested.
    /// </summary>
    public static List<CompanionPickDto> Picks(IReadOnlyList<CompanionPlaceDto> places, DateTime localNow)
    {
        // OrderBy is stable: at equal walking time, the ranking (named first) decides
        CompanionPlaceDto? Nearest(string category) => places
            .Where(p => p.Category == category && p.OpenStatus != OpeningHours.Closed)
            .OrderBy(p => p.WalkMinutes)
            .FirstOrDefault();

        var picks = new List<CompanionPickDto>();
        void Add(string label, CompanionPlaceDto? place)
        {
            if (place is not null && picks.Count < MaxPicks) picks.Add(new CompanionPickDto(label, place.Id, Reason(place)));
        }

        Add("Quick coffee", Nearest(PlaceCategories.Cafe));

        var time = TimeOnly.FromDateTime(localNow);
        var meal = time.IsBetween(LunchStart, LunchEnd.AddMinutes(1)) ? "Lunch"
                 : time.IsBetween(DinnerStart, DinnerEnd.AddMinutes(1)) ? "Dinner"
                 : null;
        if (meal is not null) Add(meal, Nearest(PlaceCategories.Restaurant));

        Add("Pray", Nearest(PlaceCategories.Mosque));

        if (picks.Count < MaxPicks) Add("Take a walk", Nearest(PlaceCategories.Park));
        return picks;
    }

    private static string Reason(CompanionPlaceDto place) =>
        place.ClosesAt is { } closes ? $"{place.WalkMinutes} min walk · open until {closes}" : $"{place.WalkMinutes} min walk";

    private static int StatusRank(string status) => status switch
    {
        OpeningHours.Open => 0,
        OpeningHours.Unknown => 1,
        _ => 2,
    };
}
