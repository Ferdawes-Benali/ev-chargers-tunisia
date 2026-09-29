using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetTopologySuite.Geometries;
using EvChargers.Application.Email;
using EvChargers.Application.Interfaces;
using EvChargers.Application.Services;
using EvChargers.Domain.Entities;
using Xunit;

namespace EvChargers.Tests;

public class UserServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IStationRepository> _stations = new();
    private readonly Mock<IEmailQueue> _emailQueue = new();
    private readonly UserService _service;

    public UserServiceTests()
    {
        _service = new UserService(_users.Object, _stations.Object, _emailQueue.Object, NullLogger<UserService>.Instance);
    }

    private void GivenNoUserYet() =>
        _users.Setup(r => r.UpsertAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

    // --- First login ---

    [Fact]
    public async Task New_user_is_inserted_and_gets_exactly_one_welcome_email()
    {
        GivenNoUserYet();

        var (profile, isNew) = await _service.EnsureUserAsync(UserId, "amira@example.com", "Amira", "https://img/a.png", "fr", CancellationToken.None);

        isNew.Should().BeTrue();
        profile.DisplayName.Should().Be("Amira");
        profile.AvatarUrl.Should().Be("https://img/a.png");
        _users.Verify(r => r.UpsertAsync(
            It.Is<AppUser>(u => u.Id == UserId && u.Email == "amira@example.com" && !u.IsAdmin),
            It.IsAny<CancellationToken>()), Times.Once);
        _emailQueue.Verify(q => q.EnqueueAsync(
            It.Is<EmailMessage>(m => m.To == "amira@example.com" && m.Html.Contains("Amira")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("ar", "lang=\"ar\"", "مرحبًا بك في EV Chargers Tunisia")]
    [InlineData("en", "lang=\"en\"", "Welcome to EV Chargers Tunisia")]
    [InlineData("fr", "lang=\"fr\"", "Bienvenue sur EV Chargers Tunisia")]
    public async Task Welcome_email_uses_the_new_users_language(string language, string htmlLang, string subject)
    {
        GivenNoUserYet();

        await _service.EnsureUserAsync(UserId, "amira@example.com", "Amira", null, language, CancellationToken.None);

        _users.Verify(r => r.UpsertAsync(It.Is<AppUser>(u => u.PreferredLanguage == language), It.IsAny<CancellationToken>()), Times.Once);
        _emailQueue.Verify(q => q.EnqueueAsync(
            It.Is<EmailMessage>(m => m.Subject == subject && m.Html.Contains(htmlLang)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task New_user_without_email_gets_no_email(string? email)
    {
        GivenNoUserYet();

        var (_, isNew) = await _service.EnsureUserAsync(UserId, email, "Amira", null, "fr", CancellationToken.None);

        isNew.Should().BeTrue();
        _emailQueue.Verify(q => q.EnqueueAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Losing_the_first_login_race_returns_stored_user_and_sends_nothing()
    {
        var winner = new AppUser { Id = UserId, DisplayName = "Stored", Email = "amira@example.com" };
        _users.SetupSequence(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null)   // not there yet...
            .ReturnsAsync(winner);          // ...but the other request inserted it first
        _users.Setup(r => r.UpsertAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var (profile, isNew) = await _service.EnsureUserAsync(UserId, "amira@example.com", "Amira", null, "fr", CancellationToken.None);

        isNew.Should().BeFalse();
        profile.DisplayName.Should().Be("Stored");
        _emailQueue.Verify(q => q.EnqueueAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Email_queue_failure_does_not_fail_login()
    {
        GivenNoUserYet();
        _emailQueue.Setup(q => q.EnqueueAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue closed"));

        var (_, isNew) = await _service.EnsureUserAsync(UserId, "amira@example.com", "Amira", null, "fr", CancellationToken.None);

        isNew.Should().BeTrue();
    }

    // --- Returning user ---

    [Fact]
    public async Task Existing_user_is_updated_and_gets_no_email()
    {
        var favorite = Guid.NewGuid();
        var stored = new AppUser
        {
            Id = UserId, Email = "old@example.com", DisplayName = "Old", AvatarUrl = "https://img/old.png",
            IsAdmin = true, PreferredLanguage = "ar", FavoriteStationIds = [favorite],
        };
        _users.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        var (profile, isNew) = await _service.EnsureUserAsync(UserId, "new@example.com", "New", "https://img/new.png", "en", CancellationToken.None);

        isNew.Should().BeFalse();
        profile.DisplayName.Should().Be("New");
        profile.IsAdmin.Should().BeTrue();
        profile.FavoriteStationIds.Should().Equal(favorite);
        profile.PreferredLanguage.Should().Be("ar"); // the stored choice, not the "en" from Accept-Language
        _users.Verify(r => r.UpsertAsync(
            It.Is<AppUser>(u => u.Email == "new@example.com" && u.DisplayName == "New" && u.AvatarUrl == "https://img/new.png"
                                && u.IsAdmin && u.PreferredLanguage == "ar"),
            It.IsAny<CancellationToken>()), Times.Once);
        _emailQueue.Verify(q => q.EnqueueAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unchanged_existing_user_is_not_saved_again()
    {
        var stored = new AppUser { Id = UserId, Email = "a@example.com", DisplayName = "A", AvatarUrl = "https://img/a.png" };
        _users.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        // A missing claim (null avatar) never erases the stored value either
        await _service.EnsureUserAsync(UserId, "a@example.com", "A", null, "fr", CancellationToken.None);

        stored.AvatarUrl.Should().Be("https://img/a.png");
        _users.Verify(r => r.UpsertAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Favorites ---

    private static Station StationNamed(string name, params int[] ratings) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Location = new Point(10.18, 36.80) { SRID = 4326 },
        Reviews = ratings.Select(r => new Review { Id = Guid.NewGuid(), Rating = r }).ToList(),
    };

    private void GivenFavorites(params Guid[] ids) =>
        _users.Setup(r => r.GetByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppUser { Id = UserId, FavoriteStationIds = [.. ids] });

    [Fact]
    public async Task Favorites_keep_the_order_the_user_saved_them_in()
    {
        var first = StationNamed("Sousse Centre", 4, 5);
        var second = StationNamed("Tunis Lac");
        var third = StationNamed("Sfax Port", 3);
        GivenFavorites(first.Id, second.Id, third.Id);
        // The database returns them in any order
        _stations.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([third, first, second]);

        var favorites = await _service.GetFavoritesAsync(UserId, CancellationToken.None);

        favorites.Select(f => f.Name).Should().Equal("Sousse Centre", "Tunis Lac", "Sfax Port");
        favorites[0].AvgRating.Should().Be(4.5);
        favorites[1].AvgRating.Should().BeNull();
        _stations.Verify(r => r.GetByIdsAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { first.Id, second.Id, third.Id })),
            It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(r => r.RemoveFavoritesAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deleted_stations_are_skipped_and_removed_from_favorites()
    {
        var kept = StationNamed("Nabeul");
        var deletedA = Guid.NewGuid();
        var deletedB = Guid.NewGuid();
        GivenFavorites(deletedA, kept.Id, deletedB);
        _stations.Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([kept]);

        var favorites = await _service.GetFavoritesAsync(UserId, CancellationToken.None);

        favorites.Should().ContainSingle(f => f.Id == kept.Id);
        _users.Verify(r => r.RemoveFavoritesAsync(UserId,
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(deletedA) && ids.Contains(deletedB)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task No_favorites_or_unknown_user_gives_an_empty_list_without_a_station_query()
    {
        GivenFavorites();
        (await _service.GetFavoritesAsync(UserId, CancellationToken.None)).Should().BeEmpty();
        (await _service.GetFavoritesAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeEmpty();

        _stations.Verify(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
