using FluentAssertions;
using EvChargers.Application.Common;
using Xunit;

namespace EvChargers.Tests;

public class OpeningHoursTests
{
    // Week of 28 Sep 2026: Mo 28, Tu 29, We 30, Th 1 Oct, Fr 2, Sa 3, Su 4
    private static DateTime At(string day, int hour, int minute = 0)
    {
        var date = day switch
        {
            "Mo" => 28, "Tu" => 29, "We" => 30, "Th" => 1, "Fr" => 2, "Sa" => 3, "Su" => 4,
            _ => throw new ArgumentException(day),
        };
        return new DateTime(2026, date >= 28 ? 9 : 10, date, hour, minute, 0);
    }

    private static OpenState Eval(string? tag, string day, int hour, int minute = 0) =>
        OpeningHours.Evaluate(tag, At(day, hour, minute));

    [Fact]
    public void Always_open()
    {
        Eval("24/7", "Su", 3).Should().Be(new OpenState("open", null, null));
        Eval("Mo-Su 00:00-24:00", "We", 23, 59).Should().Be(new OpenState("open", null, null));
    }

    [Theory]
    [InlineData("Tu", 10, 0, "open", "18:00", null)]
    [InlineData("Tu", 7, 30, "closed", null, "08:00")]    // opens later today
    [InlineData("Tu", 19, 0, "closed", null, "08:00")]    // opens tomorrow morning
    [InlineData("Sa", 12, 0, "open", "13:00", null)]
    [InlineData("Su", 10, 0, "closed", null, "08:00")]    // Monday 08:00 is 22 h away
    [InlineData("Sa", 14, 0, "closed", null, null)]       // next opening is Monday: more than 24 h, not shown
    public void Weekdays_and_saturday(string day, int hour, int minute, string status, string? closesAt, string? opensAt)
    {
        Eval("Mo-Fr 08:00-18:00; Sa 09:00-13:00", day, hour, minute)
            .Should().Be(new OpenState(status, closesAt, opensAt));
    }

    [Fact]
    public void Every_day_range()
    {
        Eval("Mo-Su 07:00-23:00", "We", 6, 59).Should().Be(new OpenState("closed", null, "07:00"));
        Eval("Mo-Su 07:00-23:00", "We", 7, 0).Should().Be(new OpenState("open", "23:00", null));
        Eval("Mo-Su 07:00-23:00", "We", 23, 0).Should().Be(new OpenState("closed", null, "07:00"));
    }

    [Fact]
    public void Day_list_with_split_hours()
    {
        const string tag = "Mo,We 10:00-12:00,14:00-18:00";
        Eval(tag, "Mo", 15).Should().Be(new OpenState("open", "18:00", null));
        Eval(tag, "We", 13).Should().Be(new OpenState("closed", null, "14:00"));
        Eval(tag, "Tu", 11).Should().Be(new OpenState("closed", null, "10:00")); // Wednesday 10:00, 23 h away
        Eval(tag, "Th", 11).Should().Be(new OpenState("closed", null, null));
    }

    [Fact]
    public void Hours_past_midnight()
    {
        const string tag = "Mo-Su 18:00-02:00";
        Eval(tag, "Tu", 23).Should().Be(new OpenState("open", "02:00", null));
        Eval(tag, "Tu", 1, 30).Should().Be(new OpenState("open", "02:00", null)); // Monday night's opening
        Eval(tag, "Tu", 3).Should().Be(new OpenState("closed", null, "18:00"));
    }

    [Fact]
    public void Past_midnight_only_after_the_listed_day()
    {
        Eval("Fr 18:00-02:00", "Sa", 1, 30).Should().Be(new OpenState("open", "02:00", null));
        Eval("Fr 18:00-02:00", "Su", 1, 30).Should().Be(new OpenState("closed", null, null));
    }

    [Theory]
    [InlineData("Mo-Sa 08:00-20:00; Su off")]
    [InlineData("Mo-Sa 08:00-20:00; Su closed")]
    public void Closed_days(string tag)
    {
        Eval(tag, "Su", 10).Should().Be(new OpenState("closed", null, "08:00")); // Monday 08:00, 22 h away
        Eval(tag, "Sa", 10).Status.Should().Be("open");
    }

    [Fact]
    public void Later_rule_replaces_earlier_one_for_its_days()
    {
        const string tag = "Mo-Su 08:00-20:00; Fr 14:00-20:00";
        Eval(tag, "Fr", 10).Should().Be(new OpenState("closed", null, "14:00"));
        Eval(tag, "Th", 10).Status.Should().Be("open");
    }

    [Fact]
    public void Bare_time_range_means_every_day()
    {
        Eval("08:00-18:00", "Su", 9).Should().Be(new OpenState("open", "18:00", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Mo-Fr 08:00-18:00; PH off")]      // public holidays
    [InlineData("sunrise-sunset")]
    [InlineData("Jan-Mar Mo 10:00-12:00")]         // months
    [InlineData("Mo-Fr 08:00+")]                   // open end
    [InlineData("Mo-Fr")]                          // days without times
    [InlineData("Mo-Fr 25:00-26:00")]              // impossible start
    [InlineData("Mo-Fr 8h-18h")]
    [InlineData("open")]
    public void Anything_else_is_unknown(string? tag)
    {
        OpeningHours.Parse(tag).Should().BeNull();
        Eval(tag, "Tu", 10).Should().Be(OpenState.Unknown);
    }

    [Fact]
    public void Tunisia_is_utc_plus_one()
    {
        TunisiaTime.ToLocal(new DateTimeOffset(2026, 9, 29, 11, 0, 0, TimeSpan.Zero))
            .Should().Be(new DateTime(2026, 9, 29, 12, 0, 0));
        // No daylight saving in Tunisia: still +1 in July
        TunisiaTime.ToLocal(new DateTimeOffset(2026, 7, 1, 23, 30, 0, TimeSpan.Zero))
            .Should().Be(new DateTime(2026, 7, 2, 0, 30, 0));
    }
}
