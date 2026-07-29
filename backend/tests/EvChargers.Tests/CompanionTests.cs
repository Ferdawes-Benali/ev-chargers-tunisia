using System.Text.Json;
using FluentAssertions;
using Moq;
using NetTopologySuite.Geometries;
using EvChargers.Application.Common;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;
using EvChargers.Application.Services;
using EvChargers.Domain.Entities;
using EvChargers.Infrastructure.External;
using Xunit;

namespace EvChargers.Tests;

public class ChargingTimeTests
{
    [Theory]
    [InlineData(22, 110)]   // AC: 36 kWh / (22 × 0.9) = 109 min
    [InlineData(7, 345)]    // AC wallbox: 343 min
    [InlineData(50, 60)]    // DC: 36 / (50 × 0.75) = 57.6 min
    [InlineData(150, 30)]   // DC capped at 100 kW: 36 / 75 = 28.8 min
    [InlineData(100, 30)]
    [InlineData(23, 125)]   // just above 22 kW counts as DC
    public void Estimates_minutes_rounded_to_5(int powerKw, int expected)
    {
        ChargingTime.EstimateMinutes(powerKw).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void No_connector_gives_null(int? powerKw)
    {
        ChargingTime.EstimateMinutes(powerKw).Should().BeNull();
    }
}

public class WalkingTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(64, 1)]     // 64 × 1.25 / 80 = 1.0
    [InlineData(65, 2)]     // just over a minute rounds up
    [InlineData(300, 5)]    // 4.7
    [InlineData(1000, 16)]  // 15.6
    public void Walk_minutes_round_up(double meters, int expected)
    {
        Walking.WalkMinutes(meters).Should().Be(expected);
    }

    [Fact]
    public void Distance_uses_haversine()
    {
        // 0.01° of latitude ≈ 1112 m
        Walking.DistanceMeters(36.80, 10.18, 36.81, 10.18).Should().BeInRange(1105, 1120);
    }
}

public class OverpassParsingTests
{
    // Trimmed from a real response around central Tunis (36.8065, 10.1815), plus a few edge cases
    private const string Sample = """
        {"elements":[
          {"type":"node","id":715619811,"lat":36.8052826,"lon":10.181076,"tags":{"amenity":"place_of_worship","name":"جامع نهج مرسيليا","name:en":"Mosque","name:fr":"Mosquée de la rue de Marseille","religion":"muslim"}},
          {"type":"node","id":1037160798,"lat":36.8051387,"lon":10.1849065,"tags":{"amenity":"bank","atm":"yes","name":"بنك الإسكان","name:fr":"Banque de l'Habitat"}},
          {"type":"node","id":1158225731,"lat":36.8024064,"lon":10.1823039,"tags":{"amenity":"restaurant","cuisine":"steak_house","name":"Steak Haus"}},
          {"type":"way","id":32900387,"center":{"lat":36.8047707,"lon":10.1782628},"tags":{"leisure":"park","name:fr":"Jardin Habib Thameur"}},
          {"type":"way","id":94346632,"center":{"lat":36.8018666,"lon":10.1833091},"tags":{"shop":"mall","name":"سنترال بارك","name:fr":"Central Park"}},
          {"type":"node","id":1,"lat":36.8060,"lon":10.1800,"tags":{"amenity":"atm"}},
          {"type":"node","id":2,"lat":36.80605,"lon":10.1800,"tags":{"amenity":"atm"}},
          {"type":"node","id":3,"lat":36.8070,"lon":10.1800,"tags":{"amenity":"cafe"}},
          {"type":"node","id":4,"lat":36.8070,"lon":10.1810,"tags":{"amenity":"place_of_worship","religion":"christian","name":"Cathédrale"}},
          {"type":"node","id":5,"lat":36.8070,"lon":10.1820}
        ]}
        """;

    [Fact]
    public void Parses_real_overpass_elements()
    {
        using var doc = JsonDocument.Parse(Sample);

        var places = OverpassPlacesProvider.Parse(doc.RootElement);

        places.Select(p => (p.Name, p.Category)).Should().Equal(
            ("جامع نهج مرسيليا", PlaceCategories.Mosque),
            ("بنك الإسكان", PlaceCategories.Atm),
            ("Steak Haus", PlaceCategories.Restaurant),
            ("Jardin Habib Thameur", PlaceCategories.Park),   // way → center point; name:fr fallback
            ("سنترال بارك", PlaceCategories.Shopping),
            ("ATM", PlaceCategories.Atm),                     // the second unnamed ATM, 5 m away, is dropped
            ("Café", PlaceCategories.Cafe));                  // unnamed → generic label
        places[3].Lat.Should().Be(36.8047707);
        places[3].Lng.Should().Be(10.1782628);
    }

    [Fact]
    public void Keeps_osm_id_named_flag_and_opening_hours()
    {
        using var doc = JsonDocument.Parse("""
            {"elements":[
              {"type":"way","id":94346632,"center":{"lat":36.80,"lon":10.18},"tags":{"shop":"mall","name":"Central Park","opening_hours":"Mo-Su 09:00-21:00"}},
              {"type":"node","id":3,"lat":36.80,"lon":10.18,"tags":{"amenity":"cafe"}},
              {"type":"node","lat":36.80,"lon":10.18,"tags":{"amenity":"cafe","name":"No id"}}
            ]}
            """);

        var places = OverpassPlacesProvider.Parse(doc.RootElement);

        places.Should().HaveCount(2); // no id → skipped: it could never be selected again
        places[0].Should().Match<RawPlace>(p => p.Id == "way/94346632" && p.IsNamed && p.OpeningHours == "Mo-Su 09:00-21:00");
        places[1].Should().Match<RawPlace>(p => p.Id == "node/3" && !p.IsNamed && p.Name == "Café" && p.OpeningHours == null);
    }

    [Fact]
    public void Collects_names_per_language()
    {
        using var doc = JsonDocument.Parse(Sample);

        var names = OverpassPlacesProvider.Parse(doc.RootElement).Select(p => p.Names).ToList();

        // name:fr and name:en tags; plain name is Arabic script and there is no name:ar → used as ar
        names[0].Should().Be(new PlaceNames("Mosquée de la rue de Marseille", "جامع نهج مرسيليا", "Mosque"));
        names[2].Should().Be(new PlaceNames(null, null, null));                  // "Steak Haus" is Latin
        names[3].Should().Be(new PlaceNames("Jardin Habib Thameur", null, null)); // no plain name at all
        names[4].Should().Be(new PlaceNames("Central Park", "سنترال بارك", null));
    }

    [Fact]
    public void Mixed_script_name_is_not_taken_as_arabic()
    {
        using var doc = JsonDocument.Parse("""
            {"elements":[{"type":"node","id":9,"lat":36.8,"lon":10.18,"tags":{"amenity":"cafe","name":"Café الياسمين"}}]}
            """);

        OverpassPlacesProvider.Parse(doc.RootElement).Single().Names.Ar.Should().BeNull();
    }

    [Fact]
    public void Query_asks_for_every_category_with_centers()
    {
        var query = OverpassPlacesProvider.BuildQuery(36.8065, 10.1815, 960);

        query.Should().Contain("[out:json][timeout:15]")
            .And.Contain("nwr(around:960,36.8065,10.1815)")
            .And.Contain("\"religion\"=\"muslim\"")
            .And.Contain("out center tags;");
    }
}
