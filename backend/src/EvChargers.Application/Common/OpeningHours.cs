using System.Text.RegularExpressions;

namespace EvChargers.Application.Common;

/// <param name="Status">"open", "closed" or "unknown".</param>
/// <param name="ClosesAt">Local "HH:mm" when open and the closing time is known.</param>
/// <param name="OpensAt">Local "HH:mm" when closed and it reopens within 24 hours.</param>
public record OpenState(string Status, string? ClosesAt, string? OpensAt)
{
    public static readonly OpenState Unknown = new(OpeningHours.Unknown, null, null);
}

/// <summary>
/// A deliberately small reader for the OSM "opening_hours" tag. Supported:
/// "24/7"; rules separated by ";" made of optional days ("Mo-Fr", "Mo,We", "Sa") and
/// times ("08:00-18:00", "10:00-12:00,14:00-18:00", "18:00-02:00" past midnight) or "off"/"closed".
/// A later rule replaces earlier ones for the days it names, as in OSM.
/// Unsupported expressions (such as public holidays, months, or sunrise) return unknown rather than assumed hours.
/// </summary>
public static partial class OpeningHours
{
    public const string Open = "open";
    public const string Closed = "closed";
    public const string Unknown = "unknown";

    private const int MinutesPerDay = 24 * 60;
    private static readonly string[] DayCodes = ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"];

    /// <summary>Parsed weekly schedule: per day (Monday first), minute intervals. End may pass 1440 (after midnight).</summary>
    public sealed record Schedule(IReadOnlyList<(int Start, int End)>[] Days, bool AlwaysOpen);

    public static OpenState Evaluate(string? tag, DateTime localNow) => Evaluate(Parse(tag), localNow);

    public static OpenState Evaluate(Schedule? schedule, DateTime localNow)
    {
        if (schedule is null) return OpenState.Unknown;
        if (schedule.AlwaysOpen) return new OpenState(Open, null, null);

        var today = DayIndex(localNow.DayOfWeek);
        var minute = localNow.Hour * 60 + localNow.Minute;

        foreach (var (start, end) in schedule.Days[today])
            if (start <= minute && minute < end) return new OpenState(Open, Format(end), null);

        // Yesterday's late opening that runs past midnight
        foreach (var (_, end) in schedule.Days[(today + 6) % 7])
            if (end > MinutesPerDay && minute < end - MinutesPerDay) return new OpenState(Open, Format(end), null);

        return new OpenState(Closed, null, NextOpeningWithin24h(schedule, today, minute));
    }

    public static Schedule? Parse(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var text = tag.Trim();
        if (text == "24/7") return AllDay();

        var days = Enumerable.Range(0, 7).Select(_ => (IReadOnlyList<(int, int)>)[]).ToArray();
        foreach (var rawRule in text.Split(';'))
        {
            var rule = rawRule.Trim();
            if (rule.Length == 0) continue;

            int[] selectedDays;
            string timesPart;
            if (rule.Length >= 2 && DayCodes.Contains(rule[..2]))
            {
                var space = rule.IndexOf(' ');
                if (space < 0) return null;
                var parsedDays = ParseDays(rule[..space]);
                if (parsedDays is null) return null;
                selectedDays = parsedDays;
                timesPart = rule[(space + 1)..].Trim();
            }
            else
            {
                selectedDays = [0, 1, 2, 3, 4, 5, 6]; // bare "08:00-18:00" = every day
                timesPart = rule;
            }

            IReadOnlyList<(int, int)>? intervals = timesPart is "off" or "closed" ? [] : ParseTimes(timesPart);
            if (intervals is null) return null;
            foreach (var day in selectedDays) days[day] = intervals;
        }

        // "Mo-Su 00:00-24:00" is 24/7 in disguise
        return days.All(d => d.Count == 1 && d[0] == (0, MinutesPerDay)) ? AllDay() : new Schedule(days, false);
    }

    private static Schedule AllDay() =>
        new(Enumerable.Range(0, 7).Select(_ => (IReadOnlyList<(int, int)>)[(0, MinutesPerDay)]).ToArray(), true);

    /// <summary>"Mo-Fr", "Mo,We", "Fr-Mo" (wraps), "Sa".</summary>
    private static int[]? ParseDays(string text)
    {
        var result = new SortedSet<int>();
        foreach (var item in text.Split(','))
        {
            var bounds = item.Split('-');
            if (bounds.Length is < 1 or > 2) return null;
            var from = Array.IndexOf(DayCodes, bounds[0]);
            var to = bounds.Length == 2 ? Array.IndexOf(DayCodes, bounds[1]) : from;
            if (from < 0 || to < 0) return null;
            for (var d = from; ; d = (d + 1) % 7)
            {
                result.Add(d);
                if (d == to) break;
            }
        }
        return [.. result];
    }

    /// <summary>"08:00-12:00,14:00-18:00". An end at or before the start runs past midnight.</summary>
    private static List<(int, int)>? ParseTimes(string text)
    {
        var intervals = new List<(int, int)>();
        foreach (var item in text.Split(','))
        {
            var match = TimeRange().Match(item.Trim());
            if (!match.Success) return null;
            var start = ToMinutes(match.Groups[1].Value, match.Groups[2].Value);
            var end = ToMinutes(match.Groups[3].Value, match.Groups[4].Value);
            if (start is null || end is null || start >= MinutesPerDay) return null;
            if (end <= start) end += MinutesPerDay;
            if (end > 2 * MinutesPerDay) return null;
            intervals.Add((start.Value, end.Value));
        }
        return intervals;
    }

    private static int? ToMinutes(string hours, string minutes)
    {
        var h = int.Parse(hours);
        var m = int.Parse(minutes);
        return m < 60 && h <= 48 ? h * 60 + m : null;
    }

    private static string? NextOpeningWithin24h(Schedule schedule, int today, int minute)
    {
        var candidates = schedule.Days[today].Where(i => i.Start > minute).Select(i => i.Start - minute)
            .Concat(schedule.Days[(today + 1) % 7].Select(i => MinutesPerDay - minute + i.Start))
            .Where(wait => wait <= MinutesPerDay)
            .ToList();
        return candidates.Count == 0 ? null : Format(minute + candidates.Min());
    }

    private static int DayIndex(DayOfWeek day) => ((int)day + 6) % 7; // Monday = 0

    private static string Format(int minutes)
    {
        var m = minutes % MinutesPerDay;
        return $"{m / 60:D2}:{m % 60:D2}";
    }

    [GeneratedRegex(@"^(\d{1,2}):(\d{2})\s*-\s*(\d{1,2}):(\d{2})$")]
    private static partial Regex TimeRange();
}
