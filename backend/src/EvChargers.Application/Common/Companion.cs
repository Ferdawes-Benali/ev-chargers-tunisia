using EvChargers.Application.DTOs;

namespace EvChargers.Application.Common;

public static class Companion
{
    public const int DefaultOneWayMinutes = 15;
    public const int MaxPerCategory = 8;
    public const int MaxTotal = 40;
    public const int MaxAtmsInDefaultList = 2;
    public const int MinRadiusMeters = 300;
    public const int MaxRadiusMeters = 1500;
    public const int MaxPicks = 3;

    private static readonly TimeOnly LunchStart = new(11, 30), LunchEnd = new(14, 30);
    private static readonly TimeOnly DinnerStart = new(18, 30), DinnerEnd = new(22, 0);

    public static int OneWayBudgetMinutes(int? chargeMinutes) =>
        chargeMinutes is { } minutes ? minutes / 2 : DefaultOneWayMinutes;

    public static int SearchRadiusMeters(int? chargeMinutes)
    {
        var radius = Walking.RadiusMeters(OneWayBudgetMinutes(chargeMinutes));
        return (int)Math.Clamp(Math.Round(radius), MinRadiusMeters, MaxRadiusMeters);
    }

    /// <summary>Walking band code; the frontend shows the translated label.</summary>
    public static string Band(int walkMinutes) => walkMinutes switch
    {
        <= 2 => WalkBands.Min2,
        <= 5 => WalkBands.Min5,
        <= 10 => WalkBands.Min10,
        <= 15 => WalkBands.Min15,
        _ => WalkBands.Far,
    };

    /// <summary>"HH:mm" local time when the car should be ready.</summary>
    public static string? BackBy(DateTime localNow, int? chargeMinutes) =>
        chargeMinutes is { } minutes ? localNow.AddMinutes(minutes).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) : null;

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
        void Add(string kind, CompanionPlaceDto? place)
        {
            if (place is not null && picks.Count < MaxPicks) picks.Add(new CompanionPickDto(kind, place.Id, place.WalkMinutes));
        }

        Add(PickKinds.Coffee, Nearest(PlaceCategories.Cafe));

        var time = TimeOnly.FromDateTime(localNow);
        var meal = time.IsBetween(LunchStart, LunchEnd.AddMinutes(1)) ? PickKinds.Lunch
                 : time.IsBetween(DinnerStart, DinnerEnd.AddMinutes(1)) ? PickKinds.Dinner
                 : null;
        if (meal is not null) Add(meal, Nearest(PlaceCategories.Restaurant));

        Add(PickKinds.Pray, Nearest(PlaceCategories.Mosque));

        if (picks.Count < MaxPicks) Add(PickKinds.Walk, Nearest(PlaceCategories.Park));
        return picks;
    }

    private static int StatusRank(string status) => status switch
    {
        OpeningHours.Open => 0,
        OpeningHours.Unknown => 1,
        _ => 2,
    };
}
