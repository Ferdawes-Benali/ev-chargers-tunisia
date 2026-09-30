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
    private static Func<HttpResponseMessage> Json(string body) => () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
    private static Func<HttpResponseMessage> Remark(string remark) =>
        Json($$"""{"elements":[],"remark":{{System.Text.Json.JsonSerializer.Serialize(remark)}}}""");

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
        var allDown = new ScriptedHandler(Status(HttpStatusCode.GatewayTimeout), Status(HttpStatusCode.GatewayTimeout), Timeout(), Timeout());

        var places = await Provider(allDown).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None);

        places.Should().ContainSingle(p => p.Name == "Café Phénicia");
        allDown.Urls.Should().HaveCount(4); // a refresh was attempted everywhere first
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
        var allDown = new ScriptedHandler(Status(HttpStatusCode.TooManyRequests), Status(HttpStatusCode.TooManyRequests),
                                          Status(HttpStatusCode.BadGateway), Timeout());

        (await Provider(allDown).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None)).Should().BeNull();
        allDown.Urls.Should().Equal(OverpassPlacesProvider.MainUrl, OverpassPlacesProvider.MainUrl,
                                    OverpassPlacesProvider.MirrorUrl, OverpassPlacesProvider.SecondMirrorUrl);
    }

    [Fact]
    public void Servers_are_tried_main_then_kumi_then_private_coffee()
    {
        OverpassPlacesProvider.ServerUrls.Should().Equal(
            "https://overpass-api.de/api/interpreter",
            "https://overpass.kumi.systems/api/interpreter",
            "https://overpass.private.coffee/api/interpreter");
    }

    [Fact]
    public async Task Busy_mirror_is_not_retried_and_the_next_mirror_answers()
    {
        var handler = new ScriptedHandler(Status(HttpStatusCode.InternalServerError), Status(HttpStatusCode.TooManyRequests), Ok());

        (await Provider(handler).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None)).Should().ContainSingle();

        handler.Urls.Should().Equal(OverpassPlacesProvider.MainUrl, OverpassPlacesProvider.MirrorUrl, OverpassPlacesProvider.SecondMirrorUrl);
    }

    /// <summary>Main server never answers; the mirrors answer at once.</summary>
    private sealed class HangingMainHandler : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        public bool MainRequestCancelled { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.ToString());
            if (request.RequestUri!.ToString() != OverpassPlacesProvider.MainUrl) return Ok()();
            try
            {
                await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, ct);
            }
            catch (OperationCanceledException)
            {
                MainRequestCancelled = true;
                throw;
            }
            throw new InvalidOperationException("unreachable");
        }
    }

    [Fact]
    public async Task Each_server_gets_its_own_timeout_then_the_next_is_tried()
    {
        var handler = new HangingMainHandler();
        var provider = new OverpassPlacesProvider(new HttpClient(handler), _cache, NullLogger<OverpassPlacesProvider>.Instance)
        {
            RetryDelay = TimeSpan.Zero,
            AttemptTimeout = TimeSpan.FromMilliseconds(100),
            Clock = _clock,
        };
        var started = DateTime.UtcNow;

        var places = await provider.GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None);

        places.Should().ContainSingle();
        handler.MainRequestCancelled.Should().BeTrue();
        // A timeout is not a "busy" answer: no retry on the main server
        handler.Urls.Should().Equal(OverpassPlacesProvider.MainUrl, OverpassPlacesProvider.MirrorUrl);
        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Default_timeout_is_30_seconds_per_attempt_just_above_the_query_timeout()
    {
        var http = new HttpClient();
        var provider = new OverpassPlacesProvider(http, _cache, NullLogger<OverpassPlacesProvider>.Instance);

        provider.AttemptTimeout.Should().Be(TimeSpan.FromSeconds(30));
        OverpassPlacesProvider.QueryTimeoutSeconds.Should().Be(25);
        provider.AttemptTimeout.Should().BeGreaterThan(TimeSpan.FromSeconds(OverpassPlacesProvider.QueryTimeoutSeconds));
        http.Timeout.Should().Be(System.Threading.Timeout.InfiniteTimeSpan);
    }

    [Fact]
    public async Task Timeout_remark_is_a_failure_and_the_next_server_is_tried()
    {
        var handler = new ScriptedHandler(Remark("runtime error: Query timed out in \"query\" at line 3 after 26 seconds."), Ok());

        var places = await Provider(handler).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None);

        places.Should().ContainSingle(p => p.Name == "Café Phénicia");
        // Not a "busy" status: no retry on the main server, straight to the mirror
        handler.Urls.Should().Equal(OverpassPlacesProvider.MainUrl, OverpassPlacesProvider.MirrorUrl);
    }

    [Fact]
    public async Task Error_remarks_everywhere_give_null_never_an_empty_list()
    {
        var handler = new ScriptedHandler(Remark("runtime error: Query run out of memory using about 2048 MB of RAM."),
                                          Remark("runtime error: Query timed out"), Remark("Timeout"));

        (await Provider(handler).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None)).Should().BeNull();
        handler.Urls.Should().Equal(OverpassPlacesProvider.ServerUrls);
    }

    [Fact]
    public async Task Error_remark_keeps_serving_the_previous_copy()
    {
        await Provider(new ScriptedHandler(Ok())).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None);
        _clock.Now += TimeSpan.FromHours(25);
        var remarks = new ScriptedHandler(Remark("runtime error: Query timed out"), Remark("runtime error: Query timed out"),
                                          Remark("runtime error: Query timed out"));

        (await Provider(remarks).GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None))
            .Should().ContainSingle(p => p.Name == "Café Phénicia");
    }

    [Theory]
    [InlineData("runtime error: Query timed out in \"query\" at line 3 after 26 seconds.", true)]
    [InlineData("runtime error: Query run out of memory using about 2048 MB of RAM.", true)]
    [InlineData("RUNTIME ERROR: something", true)]
    [InlineData("Timeout while waiting", true)]
    [InlineData("Query OUT OF MEMORY", true)]
    [InlineData("runtime remark: the area may be incomplete", false)]
    [InlineData(null, false)]
    public void Error_remarks_are_recognised_case_insensitively(string? remark, bool isError)
    {
        OverpassPlacesProvider.IsErrorRemark(remark).Should().Be(isError);
    }

    [Fact]
    public async Task Valid_empty_answer_is_returned_but_not_kept_so_the_next_call_asks_again()
    {
        var empty = new ScriptedHandler(Json("""{"elements":[]}"""), Json("""{"elements":[]}"""));
        var provider = Provider(empty);

        (await provider.GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None)).Should().NotBeNull().And.BeEmpty();
        (await provider.GetNearbyAsync(Lat, Lng, Radius, CancellationToken.None)).Should().NotBeNull().And.BeEmpty();

        empty.Urls.Should().HaveCount(2);
    }
}
