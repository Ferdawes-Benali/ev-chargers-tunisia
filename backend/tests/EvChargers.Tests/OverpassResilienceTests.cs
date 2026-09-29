using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using EvChargers.Infrastructure.External;
using Xunit;

namespace EvChargers.Tests;

public class OverpassResilienceTests
{
    private const double Lat = 36.8065, Lng = 10.1815;
    private const int Radius = 960;

    private const string OneCafe = """
        {"elements":[{"type":"node","id":1,"lat":36.8062,"lon":10.1813,"tags":{"amenity":"cafe","name":"Café Phénicia"}}]}
        """;

    /// <summary>Answers each request with the next scripted response and records where it went.</summary>
    private sealed class ScriptedHandler(params Func<HttpResponseMessage>[] script) : HttpMessageHandler
    {
        private int _next;
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.ToString());
            if (_next >= script.Length) throw new InvalidOperationException("Unexpected extra request");
            return Task.FromResult(script[_next++]());
        }
    }

    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static Func<HttpResponseMessage> Ok() => () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(OneCafe) };
    private static Func<HttpResponseMessage> Status(HttpStatusCode code) => () => new HttpResponseMessage(code) { Content = new StringContent("busy") };
    private static Func<HttpResponseMessage> Timeout() => () => throw new TaskCanceledException("HttpClient timeout");

    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly FakeClock _clock = new();

    private OverpassPlacesProvider Provider(ScriptedHandler handler) =>
        new(new HttpClient(handler), _cache, NullLogger<OverpassPlacesProvider>.Instance)
        {
            RetryDelay = TimeSpan.Zero,
            Clock = _clock,
        };

    [Theory]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task Busy_main_server_is_retried_once(HttpStatusCode busy)
    {
        var handler = new ScriptedHandler(Status(busy), Ok());

        var places = await Provider(handler).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None);

        places.Should().ContainSingle(p => p.Name == "Café Phénicia");
        handler.Urls.Should().Equal(OverpassPlacesProvider.MainUrl, OverpassPlacesProvider.MainUrl);
    }

    [Fact]
    public async Task Main_still_busy_after_retry_falls_back_to_mirror()
    {
        var handler = new ScriptedHandler(Status(HttpStatusCode.GatewayTimeout), Status(HttpStatusCode.GatewayTimeout), Ok());

        var places = await Provider(handler).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None);

        places.Should().ContainSingle();
        handler.Urls.Should().Equal(OverpassPlacesProvider.MainUrl, OverpassPlacesProvider.MainUrl, OverpassPlacesProvider.MirrorUrl);
    }

    [Fact]
    public async Task Main_timeout_or_other_error_goes_straight_to_mirror()
    {
        var timeout = new ScriptedHandler(Timeout(), Ok());
        var serverError = new ScriptedHandler(Status(HttpStatusCode.InternalServerError), Ok());

        (await Provider(timeout).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None)).Should().ContainSingle();
        // Another location, so the first result isn't served from the cache
        (await Provider(serverError).GetNearbyAsync(Lat + 1, Lng, Radius, CancellationToken.None)).Should().ContainSingle();

        timeout.Urls.Should().Equal(OverpassPlacesProvider.MainUrl, OverpassPlacesProvider.MirrorUrl);
        serverError.Urls.Should().Equal(OverpassPlacesProvider.MainUrl, OverpassPlacesProvider.MirrorUrl);
    }

    [Fact]
    public async Task Fresh_copy_is_served_from_cache_without_a_request()
    {
        await Provider(new ScriptedHandler(Ok())).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None);
        _clock.Now += TimeSpan.FromHours(23);
        var noCalls = new ScriptedHandler();

        var places = await Provider(noCalls).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None);

        places.Should().ContainSingle();
        noCalls.Urls.Should().BeEmpty();
    }

    [Fact]
    public async Task Stale_copy_is_returned_when_refresh_fails()
    {
        await Provider(new ScriptedHandler(Ok())).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None);
        _clock.Now += TimeSpan.FromHours(25);
        var allDown = new ScriptedHandler(Status(HttpStatusCode.GatewayTimeout), Status(HttpStatusCode.GatewayTimeout), Timeout());

        var places = await Provider(allDown).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None);

        places.Should().ContainSingle(p => p.Name == "Café Phénicia");
        allDown.Urls.Should().HaveCount(3); // a refresh was attempted everywhere first
    }

    [Fact]
    public async Task Stale_copy_is_replaced_when_refresh_succeeds()
    {
        await Provider(new ScriptedHandler(Ok())).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None);
        _clock.Now += TimeSpan.FromHours(25);
        var refreshed = new ScriptedHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"elements":[]}""") });

        (await Provider(refreshed).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None)).Should().BeEmpty();
        refreshed.Urls.Should().ContainSingle();
    }

    [Fact]
    public async Task Null_when_everything_fails_and_nothing_is_cached()
    {
        var allDown = new ScriptedHandler(Status(HttpStatusCode.TooManyRequests), Status(HttpStatusCode.TooManyRequests), Status(HttpStatusCode.BadGateway));

        (await Provider(allDown).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None)).Should().BeNull();
        allDown.Urls.Should().HaveCount(3);
    }
}
