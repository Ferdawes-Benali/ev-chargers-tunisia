using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetTopologySuite.Geometries;
using EvChargers.Application.Common;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;
using EvChargers.Application.Services;
using EvChargers.Domain.Entities;
using EvChargers.Infrastructure.Companion;
using EvChargers.Infrastructure.External;
using Xunit;

namespace EvChargers.Tests;

public class PlacesWarmupSelectionTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    private static PlacesWarmupCandidate Missing(Guid id, bool verified = true) => new(id, verified, false, null, null);
    private static PlacesWarmupCandidate FetchedHoursAgo(Guid id, double hours, bool verified = true) =>
        new(id, verified, true, Now.AddHours(-hours), Now.AddHours(-hours));

    [Fact]
    public void Missing_first_then_least_recently_attempted_and_fresh_skipped()
    {
        Guid stale30 = Guid.NewGuid(), stale100 = Guid.NewGuid(), missing = Guid.NewGuid(), fresh = Guid.NewGuid(),
             neverSucceeded = Guid.NewGuid();

        var selected = PlacesWarmup.Select(
        [
            FetchedHoursAgo(stale30, 30),
            FetchedHoursAgo(fresh, 23),
            FetchedHoursAgo(stale100, 100),
            new PlacesWarmupCandidate(neverSucceeded, true, true, null, Now.AddHours(-1)),
            Missing(missing),
        ], Now);

        // Never fetched but attempted an hour ago: behind stations last attempted days ago
        selected.Should().Equal(missing, stale100, stale30, neverSucceeded);
    }

    [Fact]
    public void Station_that_keeps_failing_does_not_block_others()
    {
        // 25 stale stations, one of them failing repeatedly and retried right at the end of its backoff
        var stale = Enumerable.Range(0, 25).Select(i => FetchedHoursAgo(Guid.NewGuid(), 25 + i)).ToList();
        var failing = new PlacesWarmupCandidate(Guid.NewGuid(), true, true, null, Now - PlacesWarmup.RetryDelay(5),
                                                LastAttemptFailed: true, AttemptCount: 5);

        var selected = PlacesWarmup.Select([failing, .. stale], Now);

        selected.Should().HaveCount(PlacesWarmup.MaxPerRun).And.NotContain(failing.StationId);
        selected.Should().Equal(stale.OrderBy(c => c.LastAttemptAt).Take(PlacesWarmup.MaxPerRun).Select(c => c.StationId));
    }

    [Theory]
    [InlineData(0, 30)]      // failed before the count existed: treated as a first failure
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(4, 240)]
    [InlineData(6, 960)]
    [InlineData(7, 1440)]    // 1920 min capped at 24 h
    [InlineData(50, 1440)]
    [InlineData(int.MaxValue, 1440)]
    public void Retry_delay_doubles_after_each_failure_capped_at_24_hours(int failures, int minutes)
    {
        PlacesWarmup.RetryDelay(failures).Should().Be(TimeSpan.FromMinutes(minutes));
    }

    [Theory]
    [InlineData(1, 29, false)]
    [InlineData(1, 30, true)]
    [InlineData(3, 119, false)]
    [InlineData(3, 120, true)]
    [InlineData(10, 23 * 60, false)]
    [InlineData(10, 24 * 60, true)]
    public void Warmup_waits_the_backoff_delay_for_the_failure_count(int failures, int minutesSinceAttempt, bool due)
    {
        var id = Guid.NewGuid();
        var failed = new PlacesWarmupCandidate(id, true, true, null, Now.AddMinutes(-minutesSinceAttempt),
                                               LastAttemptFailed: true, AttemptCount: failures);

        PlacesWarmup.Select([failed], Now).Should().Equal(due ? [id] : []);
    }

    [Fact]
    public void Only_verified_stations_are_warmed()
    {
        var verified = Guid.NewGuid();

        var selected = PlacesWarmup.Select(
        [
            Missing(Guid.NewGuid(), verified: false),
            FetchedHoursAgo(Guid.NewGuid(), 48, verified: false),
            Missing(verified),
        ], Now);

        selected.Should().Equal(verified);
    }

    [Fact]
    public void At_most_20_per_run_missing_ones_first()
    {
        var missing = Enumerable.Range(0, 15).Select(_ => Guid.NewGuid()).ToList();
        var stale = Enumerable.Range(0, 15).Select(i => (Id: Guid.NewGuid(), Hours: 25.0 + i)).ToList();

        var selected = PlacesWarmup.Select(
            missing.Select(id => Missing(id)).Concat(stale.Select(s => FetchedHoursAgo(s.Id, s.Hours))), Now);

        selected.Should().HaveCount(PlacesWarmup.MaxPerRun).And.HaveCount(20);
        selected.Take(15).Should().BeEquivalentTo(missing);
        // The 5 oldest of the stale ones
        selected.Skip(15).Should().Equal(stale.OrderByDescending(s => s.Hours).Take(5).Select(s => s.Id));
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(120, true)]
    public void Failed_entry_without_data_is_retried_after_30_minutes_not_24_hours(int minutesSinceAttempt, bool due)
    {
        var id = Guid.NewGuid();
        var failed = new PlacesWarmupCandidate(id, true, true, null, Now.AddMinutes(-minutesSinceAttempt),
                                               LastAttemptFailed: true, AttemptCount: 1);

        PlacesWarmup.Select([failed], Now).Should().Equal(due ? [id] : []);
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    public void Failed_refresh_of_stale_places_is_retried_after_30_minutes(int minutesSinceAttempt, bool due)
    {
        var id = Guid.NewGuid();
        var failed = new PlacesWarmupCandidate(id, true, true, Now.AddDays(-2), Now.AddMinutes(-minutesSinceAttempt),
                                               LastAttemptFailed: true, AttemptCount: 1);

        PlacesWarmup.Select([failed], Now).Should().Equal(due ? [id] : []);
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public void Valid_empty_result_is_rechecked_after_6_hours(int hoursSinceFetch, bool due)
    {
        var id = Guid.NewGuid();
        var empty = new PlacesWarmupCandidate(id, true, true, Now.AddHours(-hoursSinceFetch), Now.AddHours(-hoursSinceFetch),
                                              NoPlaces: true);

        PlacesWarmup.Select([empty], Now).Should().Equal(due ? [id] : []);
        // With places, the same age is still fresh
        PlacesWarmup.Select([empty with { NoPlaces = false }], Now).Should().BeEmpty();
    }
}

public class PlacesCacheRefreshTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly RawPlace Cafe =
        new("node/42", "Café Tunis", true, PlaceCategories.Cafe, 36.8070, 10.1815, PlaceNames.None);

    private readonly Mock<IStationRepository> _stations = new();
    private readonly Mock<IPlacesProvider> _provider = new();
    private readonly Mock<IPlacesCacheRepository> _cache = new();
    private readonly PlacesCacheRefresher _refresher;
    private readonly Station _station = new()
    {
        Id = Guid.NewGuid(),
        Name = "Tunis Centre",
        Location = new Point(10.1815, 36.8065) { SRID = 4326 },
        Connectors = [new Connector { PowerKw = 50 }],
    };

    public PlacesCacheRefreshTests()
    {
        _stations.Setup(r => r.GetByIdAsync(_station.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_station);
        _cache.Setup(c => c.TryStartRefreshAsync(_station.Id, Now.UtcDateTime, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _refresher = new PlacesCacheRefresher(_stations.Object, _provider.Object, _cache.Object, new FixedClock(),
                                              NullLogger<PlacesCacheRefresher>.Instance);
    }

    [Fact]
    public async Task Success_stores_the_places_fetched_with_the_companion_radius()
    {
        // 50 kW → 60 min charge → 30 min each way, capped at 1500 m
        _provider.Setup(p => p.GetNearbyAsync(36.8065, 10.1815, 1500, It.IsAny<CancellationToken>())).ReturnsAsync([Cafe]);

        (await _refresher.RefreshAsync(_station.Id, CancellationToken.None)).Should().BeTrue();

        _cache.Verify(c => c.SaveSuccessAsync(_station.Id, It.Is<List<RawPlace>>(l => l.Single() == Cafe), Now.UtcDateTime,
                                              It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.SaveFailureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Valid_empty_result_is_stored_as_a_success()
    {
        _provider.Setup(p => p.GetNearbyAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        (await _refresher.RefreshAsync(_station.Id, CancellationToken.None)).Should().BeTrue();

        _cache.Verify(c => c.SaveSuccessAsync(_station.Id, It.Is<List<RawPlace>>(l => l.Count == 0), Now.UtcDateTime,
                                              It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.SaveFailureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Failure_records_the_error_and_does_not_overwrite_places()
    {
        _provider.Setup(p => p.GetNearbyAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<RawPlace>?)null);

        (await _refresher.RefreshAsync(_station.Id, CancellationToken.None)).Should().BeFalse();

        _cache.Verify(c => c.SaveFailureAsync(_station.Id, PlacesCacheRefresher.ProviderUnavailableError, Now.UtcDateTime,
                                              It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.SaveSuccessAsync(It.IsAny<Guid>(), It.IsAny<List<RawPlace>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Recorded_failure_keeps_the_previous_places()
    {
        var fetchedAt = Now.UtcDateTime.AddDays(-2);
        var entry = new PlacesCacheEntry { StationId = _station.Id };
        entry.RecordSuccess("""[{"id":"node/42"}]""", fetchedAt);

        entry.RecordFailure(new string('x', 1000), Now.UtcDateTime);

        entry.PlacesJson.Should().Be("""[{"id":"node/42"}]""");
        entry.FetchedAt.Should().Be(fetchedAt);
        entry.LastAttemptAt.Should().Be(Now.UtcDateTime);
        entry.LastError.Should().HaveLength(PlacesCacheEntry.MaxErrorLength);
        entry.AttemptCount.Should().Be(1);
    }

    [Fact]
    public void Failures_in_a_row_are_counted_and_a_success_resets_the_count()
    {
        var entry = new PlacesCacheEntry();

        entry.RecordFailure("timed out", Now.UtcDateTime.AddHours(-1));
        entry.RecordFailure("timed out", Now.UtcDateTime.AddMinutes(-30));
        entry.AttemptCount.Should().Be(2);
        entry.PlacesJson.Should().BeNull();

        entry.RecordSuccess("[]", Now.UtcDateTime);
        entry.AttemptCount.Should().Be(0);
    }

    [Fact]
    public void Success_after_a_failure_clears_the_error()
    {
        var entry = new PlacesCacheEntry();
        entry.RecordFailure("timed out", Now.UtcDateTime.AddHours(-1));

        entry.RecordSuccess("[]", Now.UtcDateTime);

        entry.LastError.Should().BeNull();
        entry.FetchedAt.Should().Be(Now.UtcDateTime);
    }

    [Fact]
    public async Task Station_claimed_by_another_worker_is_not_fetched_again()
    {
        _cache.Setup(c => c.TryStartRefreshAsync(_station.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        (await _refresher.RefreshAsync(_station.Id, CancellationToken.None)).Should().BeFalse();

        _provider.VerifyNoOtherCalls();
        _cache.Verify(c => c.SaveSuccessAsync(It.IsAny<Guid>(), It.IsAny<List<RawPlace>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.SaveFailureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Recording_the_attempt_releases_the_claim()
    {
        var entry = new PlacesCacheEntry { RefreshStartedAt = Now.UtcDateTime.AddMinutes(-1) };
        entry.RecordFailure("timed out", Now.UtcDateTime);
        entry.RefreshStartedAt.Should().BeNull();

        entry.RefreshStartedAt = Now.UtcDateTime;
        entry.RecordSuccess("[]", Now.UtcDateTime);
        entry.RefreshStartedAt.Should().BeNull();
    }

    [Fact]
    public void Claim_outlives_a_full_server_chain()
    {
        // 4 requests of 30 s + the busy-retry pause must end well before the claim expires
        var chain = 4 * new OverpassPlacesProvider(new HttpClient(), new Microsoft.Extensions.Caching.Memory.MemoryCache(
            new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()), NullLogger<OverpassPlacesProvider>.Instance).AttemptTimeout
            + TimeSpan.FromSeconds(1.5);

        PlacesWarmup.RefreshClaimExpiresAfter.Should().BeGreaterThan(chain * 2);
    }

    [Fact]
    public async Task Unknown_station_is_skipped()
    {
        (await _refresher.RefreshAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeFalse();

        _provider.VerifyNoOtherCalls();
        _cache.VerifyNoOtherCalls();
    }
}

public class CompanionWarmupServiceTests
{
    private readonly ChannelPlacesRefreshQueue _queue = new();
    private readonly Mock<IPlacesCacheRepository> _cache = new();
    private readonly Mock<IPlacesCacheRefresher> _refresher = new();
    private readonly List<Guid> _refreshed = [];
    private int _running, _maxConcurrent;

    private CompanionWarmupService Service(TimeSpan startupDelay, int expectedRefreshes, TaskCompletionSource done)
    {
        _refresher.Setup(r => r.RefreshAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid id, CancellationToken _) =>
            {
                _maxConcurrent = Math.Max(_maxConcurrent, Interlocked.Increment(ref _running));
                await Task.Delay(10);
                lock (_refreshed) _refreshed.Add(id);
                Interlocked.Decrement(ref _running);
                if (_refreshed.Count == expectedRefreshes) done.TrySetResult();
                return true;
            });
        var services = new ServiceCollection()
            .AddSingleton(_cache.Object)
            .AddSingleton(_refresher.Object)
            .BuildServiceProvider();
        return new CompanionWarmupService(_queue, services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
                                          NullLogger<CompanionWarmupService>.Instance)
        {
            StartupDelay = startupDelay,
            PauseBetweenStations = TimeSpan.Zero,
        };
    }

    [Fact]
    public async Task Queued_station_is_refreshed_without_waiting_for_the_sweep()
    {
        var done = new TaskCompletionSource();
        var service = Service(startupDelay: TimeSpan.FromHours(1), expectedRefreshes: 1, done);
        var stationId = Guid.NewGuid();

        await service.StartAsync(CancellationToken.None);
        _queue.Request(stationId);
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        _refreshed.Should().Equal(stationId);
        _cache.Verify(c => c.GetWarmupCandidatesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sweep_refreshes_selected_stations_one_at_a_time_stalest_first()
    {
        var now = DateTime.UtcNow;
        Guid missing = Guid.NewGuid(), stale = Guid.NewGuid(), fresh = Guid.NewGuid(), pending = Guid.NewGuid();
        _cache.Setup(c => c.GetWarmupCandidatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new PlacesWarmupCandidate(stale, true, true, now.AddDays(-2), now.AddDays(-2)),
            new PlacesWarmupCandidate(fresh, true, true, now.AddHours(-1), now.AddHours(-1)),
            new PlacesWarmupCandidate(pending, false, false, null, null),
            new PlacesWarmupCandidate(missing, true, false, null, null),
        ]);
        var done = new TaskCompletionSource();
        var service = Service(startupDelay: TimeSpan.Zero, expectedRefreshes: 2, done);

        await service.StartAsync(CancellationToken.None);
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        _refreshed.Should().Equal(missing, stale);
        _maxConcurrent.Should().Be(1);
    }

    [Fact]
    public void Runs_every_30_minutes_so_failed_stations_are_retried_at_the_next_run()
    {
        var service = new CompanionWarmupService(_queue, new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                                                 TimeProvider.System, NullLogger<CompanionWarmupService>.Instance);

        service.SweepInterval.Should().Be(PlacesWarmup.RetryFailedAfter).And.Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Queue_holds_each_station_once()
    {
        var id = Guid.NewGuid();

        _queue.Request(id).Should().BeTrue();
        _queue.Request(id).Should().BeFalse();
        _queue.TryDequeue(out var first).Should().BeTrue();
        first.Should().Be(id);
        _queue.TryDequeue(out _).Should().BeFalse();
        // Once taken, it can be asked for again
        _queue.Request(id).Should().BeTrue();
    }
}
