using FluentAssertions;
using Moq;
using NetTopologySuite.Geometries;
using EvChargers.Application.Common;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;
using EvChargers.Application.Services;
using EvChargers.Domain.Entities;
using Xunit;

namespace EvChargers.Tests;

public class CompanionBuildTests
{
    private const double Lat = 36.8065, Lng = 10.1815;
    private static readonly DateTime Tuesday10 = new(2026, 9, 29, 10, 0, 0);

    /// <summary>A place <paramref name="meters"/> north of the station.</summary>
    private static RawPlace At(double meters, string name = "Place", string category = PlaceCategories.Cafe,
                               string? hours = null, bool named = true) =>
        new($"node/{name}", name, named, category, Lat + meters / 111_195.0, Lng, PlaceNames.None, hours);

    private static List<CompanionPlaceDto> Build(IEnumerable<RawPlace> places, int? chargeMinutes = 60, DateTime? now = null) =>
        Companion.Build(Lat, Lng, places, chargeMinutes, now ?? Tuesday10);

    [Theory]
    [InlineData(null, 960)]   // 15 min one way × 80 / 1.25
    [InlineData(30, 960)]     // 15 min each way
    [InlineData(10, 320)]
    [InlineData(6, 300)]      // at least 300 m
    [InlineData(110, 1500)]   // at most 1500 m
    public void Search_radius_follows_the_walk_budget(int? chargeMinutes, int expected)
    {
        Companion.SearchRadiusMeters(chargeMinutes).Should().Be(expected);
    }

    [Fact]
    public void Keeps_only_places_whose_round_trip_fits_the_charge()
    {
        var places = new[] { At(100, "A"), At(300, "B"), At(600, "C"), At(900, "D") }; // 2, 5, 10, 15 min

        var result = Build(places, chargeMinutes: 20);

        result.Select(p => p.Name).Should().Equal("A", "B", "C");
        result.Select(p => p.WalkMinutes).Should().Equal(2, 5, 10);
    }

    [Fact]
    public void Without_charge_time_keeps_15_minutes_one_way()
    {
        var places = new[] { At(900, "Near enough"), At(1200, "Too far") }; // 15, 19 min

        Build(places, chargeMinutes: null).Select(p => p.Name).Should().Equal("Near enough");
    }

    [Fact]
    public void Same_status_sorts_by_walk_time_then_name()
    {
        var places = new[] { At(600, "Zitouna"), At(100, "beta"), At(100, "Alpha"), At(300, "Carthage") };

        Build(places).Select(p => p.Name).Should().Equal("Alpha", "beta", "Carthage", "Zitouna");
    }

    [Fact]
    public void Ranks_named_then_open_unknown_closed_then_walk_time()
    {
        var places = new[]
        {
            At(100, "Café", named: false, hours: "24/7"),         // unnamed: last despite open and nearest
            At(150, "Closed nearby", hours: "Mo-Su 18:00-23:00"), // closed at 10:00
            At(600, "Open far", hours: "Mo-Su 08:00-20:00"),
            At(300, "Unknown hours"),
            At(200, "Open near", hours: "24/7"),
        };

        Build(places).Select(p => (p.Name, p.OpenStatus)).Should().Equal(
            ("Open near", "open"),
            ("Open far", "open"),
            ("Unknown hours", "unknown"),
            ("Closed nearby", "closed"),   // closed places stay, after the others
            ("Café", "open"));
    }

    [Fact]
    public void Carries_opening_times_group_and_id()
    {
        var place = Build([At(100, "Le Baghdad", PlaceCategories.Restaurant, "Mo-Su 08:00-22:00")]).Single();

        place.Should().Match<CompanionPlaceDto>(p =>
            p.Id == "node/Le Baghdad" && p.Group == PlaceGroups.Eat
            && p.OpenStatus == "open" && p.ClosesAt == "22:00" && p.OpensAt == null);
    }

    [Theory]
    [InlineData(PlaceCategories.Cafe, PlaceGroups.Eat)]
    [InlineData(PlaceCategories.Restaurant, PlaceGroups.Eat)]
    [InlineData(PlaceCategories.Mosque, PlaceGroups.Pray)]
    [InlineData(PlaceCategories.Toilets, PlaceGroups.Essentials)]
    [InlineData(PlaceCategories.Pharmacy, PlaceGroups.Essentials)]
    [InlineData(PlaceCategories.Atm, PlaceGroups.Essentials)]
    [InlineData(PlaceCategories.Park, PlaceGroups.Relax)]
    [InlineData(PlaceCategories.Shopping, PlaceGroups.Shop)]
    public void Categories_map_to_intent_groups(string category, string group)
    {
        PlaceGroups.Of(category).Should().Be(group);
    }

    [Fact]
    public void Only_two_atms_in_the_default_list()
    {
        var atms = Enumerable.Range(1, 5).Select(i => At(i * 50, $"Bank {i}", PlaceCategories.Atm));
        var pharmacy = At(400, "Pharmacie", PlaceCategories.Pharmacy);

        var result = Build([.. atms, pharmacy]);

        result.Should().HaveCount(6); // the essentials filter still sees all five
        result.Where(p => p.InDefaultList).Select(p => p.Name).Should().Equal("Bank 1", "Bank 2", "Pharmacie");
    }

    [Fact]
    public void Caps_8_per_category_and_40_in_total()
    {
        string[] categories =
        [
            PlaceCategories.Cafe, PlaceCategories.Restaurant, PlaceCategories.Mosque, PlaceCategories.Park,
            PlaceCategories.Shopping, PlaceCategories.Pharmacy, PlaceCategories.Toilets, PlaceCategories.Atm,
        ];
        var places = categories.SelectMany(c => Enumerable.Range(0, 10).Select(i => At(100 + i * 10, $"{c} {i:D2}", c))).ToList();

        var result = Build(places);

        result.Should().HaveCount(40);
        result.GroupBy(p => p.Category).Should().OnlyContain(g => g.Count() <= 8);
        Build(places.Where(p => p.Category == PlaceCategories.Cafe)).Should().HaveCount(8);
    }

    [Fact]
    public void Assigns_bands()
    {
        var places = new[] { At(100, "a"), At(300, "b"), At(600, "c"), At(900, "d"), At(1200, "e") };

        Build(places).Select(p => p.Band).Should().Equal("min2", "min5", "min10", "min15", "far");
    }

    [Theory]
    [InlineData(10, 50, 60, "11:50")]
    [InlineData(23, 30, 60, "00:30")] // past midnight
    public void Back_by_is_now_plus_charge_time(int hour, int minute, int chargeMinutes, string expected)
    {
        Companion.BackBy(new DateTime(2026, 9, 29, hour, minute, 0), chargeMinutes).Should().Be(expected);
    }

    [Fact]
    public void No_back_by_without_charge_time()
    {
        Companion.BackBy(Tuesday10, null).Should().BeNull();
    }
}

public class CompanionPicksTests
{
    private const double Lat = 36.8065, Lng = 10.1815;

    private static RawPlace At(double meters, string name, string category, string? hours = null) =>
        new($"node/{name}", name, true, category, Lat + meters / 111_195.0, Lng, PlaceNames.None, hours);

    private static readonly RawPlace[] Town =
    [
        At(100, "Café du Coin", PlaceCategories.Cafe, "Mo-Su 06:00-23:00"),
        At(250, "Café Loin", PlaceCategories.Cafe),
        At(200, "Dar El Jeld", PlaceCategories.Restaurant, "Mo-Su 12:00-15:00,19:00-23:00"),
        At(300, "Chez Nous", PlaceCategories.Restaurant),
        At(350, "Mosquée Youssef Dey", PlaceCategories.Mosque),
        At(500, "Jardin Thameur", PlaceCategories.Park),
    ];

    private static List<(string Kind, string PlaceId)> PicksAt(int hour, int minute, IEnumerable<RawPlace>? places = null)
    {
        var now = new DateTime(2026, 9, 29, hour, minute, 0);
        var built = Companion.Build(Lat, Lng, places ?? Town, 60, now);
        return Companion.Picks(built, now).Select(p => (p.Kind, p.PlaceId)).ToList();
    }

    [Fact]
    public void Lunch_time_suggests_coffee_lunch_and_prayer()
    {
        PicksAt(12, 30).Should().Equal(
            ("coffee", "node/Café du Coin"),
            ("lunch", "node/Dar El Jeld"),
            ("pray", "node/Mosquée Youssef Dey"));
    }

    [Fact]
    public void Dinner_window_suggests_dinner()
    {
        PicksAt(19, 30).Should().Contain(("dinner", "node/Dar El Jeld"));
    }

    [Theory]
    [InlineData(11, 29)]
    [InlineData(16, 0)]
    [InlineData(22, 1)]
    public void Outside_meal_windows_a_walk_takes_the_free_slot(int hour, int minute)
    {
        PicksAt(hour, minute).Should().Equal(
            ("coffee", "node/Café du Coin"),
            ("pray", "node/Mosquée Youssef Dey"),
            ("walk", "node/Jardin Thameur"));
    }

    [Fact]
    public void Closed_places_are_never_picked()
    {
        // 05:00: Café du Coin (06:00-23:00) is closed → the farther café with unknown hours is picked
        PicksAt(5, 0)[0].Should().Be(("coffee", "node/Café Loin"));
    }

    [Fact]
    public void Park_replaces_a_missing_mosque()
    {
        var noMosque = Town.Where(p => p.Category != PlaceCategories.Mosque);

        PicksAt(12, 30, noMosque).Should().Equal(
            ("coffee", "node/Café du Coin"),
            ("lunch", "node/Dar El Jeld"),
            ("walk", "node/Jardin Thameur"));
    }

    [Fact]
    public void Picks_carry_the_walking_time_not_display_text()
    {
        var now = new DateTime(2026, 9, 29, 12, 30, 0);
        var built = Companion.Build(Lat, Lng, Town, 60, now);

        Companion.Picks(built, now)[0].Should().Be(new CompanionPickDto("coffee", "node/Café du Coin", 2));
    }

    [Fact]
    public void Nothing_to_pick_gives_no_picks()
    {
        PicksAt(12, 0, [At(100, "Pharmacie", PlaceCategories.Pharmacy)]).Should().BeEmpty();
    }
}

public class CompanionServiceTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private readonly Mock<IStationRepository> _stations = new();
    private readonly Mock<IPlacesProvider> _places = new();
    private readonly Mock<IRoutingProvider> _routing = new();
    private readonly CompanionService _service;
    private readonly Station _station = new()
    {
        Id = Guid.NewGuid(),
        Name = "Tunis Centre",
        Location = new Point(10.1815, 36.8065) { SRID = 4326 },
        Connectors = [new Connector { PowerKw = 22 }, new Connector { PowerKw = 50 }],
    };
    private static readonly RawPlace Cafe =
        new("node/42", "Café Tunis", true, PlaceCategories.Cafe, 36.8070, 10.1815, PlaceNames.None, "24/7");

    public CompanionServiceTests()
    {
        _stations.Setup(r => r.GetByIdAsync(_station.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_station);
        // 11:00 UTC = 12:00 in Tunis
        _service = new CompanionService(_stations.Object, _places.Object, _routing.Object,
                                        new FixedClock(new DateTimeOffset(2026, 9, 29, 11, 0, 0, TimeSpan.Zero)));
    }

    private void GivenPlaces(List<RawPlace>? places) =>
        _places.Setup(p => p.GetNearbyAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(places);

    [Fact]
    public async Task Uses_the_most_powerful_connector_and_local_time()
    {
        GivenPlaces([Cafe]);

        var result = (await _service.GetAsync(_station.Id, CancellationToken.None))!;

        result.MaxPowerKw.Should().Be(50);
        result.ChargeMinutes.Should().Be(60);
        result.BackBy.Should().Be("13:00");
        result.Unavailable.Should().BeFalse();
        result.Source.Should().Be("OpenStreetMap");
        result.Places.Should().ContainSingle(p => p.Id == "node/42" && p.OpenStatus == "open");
        result.Picks.Should().ContainSingle(p => p.Kind == "coffee" && p.PlaceId == "node/42");
        // 30 min each way → 1920 m, capped at 1500
        _places.Verify(p => p.GetNearbyAsync(36.8065, 10.1815, 1500, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Provider_failure_gives_unavailable_with_no_places()
    {
        GivenPlaces(null);

        var result = (await _service.GetAsync(_station.Id, CancellationToken.None))!;

        result.Unavailable.Should().BeTrue();
        result.Places.Should().BeEmpty();
        result.Picks.Should().BeEmpty();
        result.BackBy.Should().Be("13:00");
    }

    [Fact]
    public async Task Station_without_connectors_uses_the_15_minute_walk()
    {
        _station.Connectors.Clear();
        GivenPlaces([]);

        var result = (await _service.GetAsync(_station.Id, CancellationToken.None))!;

        result.ChargeMinutes.Should().BeNull();
        result.MaxPowerKw.Should().BeNull();
        result.BackBy.Should().BeNull();
        _places.Verify(p => p.GetNearbyAsync(It.IsAny<double>(), It.IsAny<double>(), 960, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unknown_station_gives_null()
    {
        (await _service.GetAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
        (await _service.GetWalkingRouteAsync(Guid.NewGuid(), "node/42", CancellationToken.None)).Should().BeNull();
    }

    // --- Walking route ---

    [Fact]
    public async Task Route_uses_ors_foot_walking_and_downsamples()
    {
        GivenPlaces([Cafe]);
        var points = Enumerable.Range(0, 500).Select(i => new[] { 36.8065 + i * 1e-6, 10.1815 }).ToList();
        _routing.Setup(r => r.GetRouteAsync(36.8065, 10.1815, 36.8070, 10.1815, It.IsAny<CancellationToken>(), RoutingProfiles.Foot))
            .ReturnsAsync(new RouteData(0.0712, 0.9, points, 0));

        var route = (await _service.GetWalkingRouteAsync(_station.Id, "node/42", CancellationToken.None))!;

        route.Estimated.Should().BeFalse();
        route.DistanceMeters.Should().Be(71);
        route.DurationMinutes.Should().Be(1);
        route.Points.Should().HaveCount(200);
        route.Points[0].Should().Equal(points[0]);
        route.Points[^1].Should().Equal(points[^1]);
    }

    [Fact]
    public async Task Route_failure_falls_back_to_the_estimate()
    {
        GivenPlaces([Cafe]);
        _routing.Setup(r => r.GetRouteAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(),
                                            It.IsAny<CancellationToken>(), It.IsAny<string>()))
            .ReturnsAsync((RouteData?)null);

        var route = (await _service.GetWalkingRouteAsync(_station.Id, "node/42", CancellationToken.None))!;

        // 56 m straight line → 70 m walked, 1 min
        route.Should().Match<WalkingRouteDto>(r => r.Estimated && r.Points.Count == 0 && r.DurationMinutes == 1 && r.DistanceMeters == 70);
    }

    [Fact]
    public async Task Route_to_unknown_place_gives_null()
    {
        GivenPlaces([Cafe]);

        (await _service.GetWalkingRouteAsync(_station.Id, "node/999", CancellationToken.None)).Should().BeNull();
        _routing.VerifyNoOtherCalls();
    }
}
