using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetTopologySuite.Geometries;
using EvChargers.Application.Common;
using EvChargers.Application.DTOs;
using EvChargers.Application.Email;
using EvChargers.Application.Interfaces;
using EvChargers.Application.Services;
using EvChargers.Domain.Entities;
using Xunit;

namespace EvChargers.Tests;

public class StationServiceTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    private readonly Mock<IStationRepository> _stations = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IAuditLogRepository> _auditLog = new();
    private readonly Mock<IEmailQueue> _emailQueue = new();
    private readonly Mock<IPlacesRefreshQueue> _placesRefresh = new();
    private readonly StationService _service;
    private readonly Station _station;

    private static readonly CreateStationRequest UpdateRequest =
        new("Renamed", "Somewhere", 36.8, 10.18, null, []);

    public StationServiceTests()
    {
        _station = new Station
        {
            Id = Guid.NewGuid(),
            Name = "Original",
            Location = new Point(10.1815, 36.8065) { SRID = 4326 },
            SubmittedBy = Owner,
        };
        _stations.Setup(r => r.GetByIdAsync(_station.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_station);

        _users.Setup(r => r.GetByIdAsync(Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppUser { Id = Admin, IsAdmin = true });
        _users.Setup(r => r.GetByIdAsync(Stranger, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppUser { Id = Stranger, IsAdmin = false, Email = "stranger@example.com", DisplayName = "Sami", PreferredLanguage = "en" });

        _service = new StationService(_stations.Object, _users.Object, _auditLog.Object,
            _emailQueue.Object, _placesRefresh.Object, NullLogger<StationService>.Instance);
    }

    // --- Update: submitter or admin ---

    [Fact]
    public async Task Owner_can_update_their_station()
    {
        var result = await _service.UpdateAsync(_station.Id, UpdateRequest, Owner, CancellationToken.None);

        result.Should().Be(OperationResult.Ok);
        _station.Name.Should().Be("Renamed");
        _stations.Verify(r => r.UpdateAsync(_station, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admin_can_update_any_station()
    {
        var result = await _service.UpdateAsync(_station.Id, UpdateRequest, Admin, CancellationToken.None);

        result.Should().Be(OperationResult.Ok);
    }

    [Fact]
    public async Task Other_user_cannot_update()
    {
        var result = await _service.UpdateAsync(_station.Id, UpdateRequest, Stranger, CancellationToken.None);

        result.Should().Be(OperationResult.Forbidden);
        _station.Name.Should().Be("Original");
        _stations.Verify(r => r.UpdateAsync(It.IsAny<Station>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Station_without_submitter_is_admin_only()
    {
        _station.SubmittedBy = null;

        (await _service.UpdateAsync(_station.Id, UpdateRequest, Stranger, CancellationToken.None))
            .Should().Be(OperationResult.Forbidden);
        (await _service.UpdateAsync(_station.Id, UpdateRequest, Admin, CancellationToken.None))
            .Should().Be(OperationResult.Ok);
    }

    [Fact]
    public async Task Updating_unknown_station_returns_not_found()
    {
        var result = await _service.UpdateAsync(Guid.NewGuid(), UpdateRequest, Admin, CancellationToken.None);

        result.Should().Be(OperationResult.NotFound);
    }

    [Fact]
    public async Task Create_records_the_submitter()
    {
        Station? saved = null;
        _stations.Setup(r => r.AddAsync(It.IsAny<Station>(), It.IsAny<CancellationToken>()))
            .Callback<Station, CancellationToken>((s, _) => saved = s);

        await _service.CreateAsync(UpdateRequest, Owner, CancellationToken.None);

        saved!.SubmittedBy.Should().Be(Owner);
    }

    // --- Verify / delete: admin only, audited ---

    [Fact]
    public async Task Non_admin_cannot_verify()
    {
        var result = await _service.VerifyAsync(_station.Id, Stranger, CancellationToken.None);

        result.Should().Be(OperationResult.Forbidden);
        _stations.Verify(r => r.UpdateAsync(It.IsAny<Station>(), It.IsAny<CancellationToken>()), Times.Never);
        _auditLog.Verify(r => r.LogAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Never);
        _placesRefresh.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Admin_verify_succeeds_and_writes_audit_log()
    {
        var result = await _service.VerifyAsync(_station.Id, Admin, CancellationToken.None);

        result.Should().Be(OperationResult.Ok);
        _auditLog.Verify(r => r.LogAsync(
            It.Is<AuditLog>(a => a.UserId == Admin && a.Action == "VerifyStation" && a.TargetId == _station.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admin_verify_queues_the_nearby_places_fetch()
    {
        await _service.VerifyAsync(_station.Id, Admin, CancellationToken.None);

        _placesRefresh.Verify(q => q.Request(_station.Id), Times.Once);
    }

    [Fact]
    public async Task Non_admin_cannot_delete()
    {
        var result = await _service.DeleteAsync(_station.Id, Owner, CancellationToken.None);

        result.Should().Be(OperationResult.Forbidden);
        _stations.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Admin_delete_succeeds_and_writes_audit_log()
    {
        var result = await _service.DeleteAsync(_station.Id, Admin, CancellationToken.None);

        result.Should().Be(OperationResult.Ok);
        _stations.Verify(r => r.DeleteAsync(_station.Id, It.IsAny<CancellationToken>()), Times.Once);
        _auditLog.Verify(r => r.LogAsync(
            It.Is<AuditLog>(a => a.UserId == Admin && a.Action == "DeleteStation" && a.TargetId == _station.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Reviews: one per user per station ---

    [Fact]
    public async Task First_review_is_created_with_its_author()
    {
        var result = await _service.AddReviewAsync(_station.Id, new CreateReviewRequest(4, "Good"), Stranger, CancellationToken.None);

        result.Should().Be(ReviewUpsertResult.Created);
        _stations.Verify(r => r.AddReviewAsync(
            It.Is<Review>(rv => rv.UserId == Stranger && rv.Rating == 4 && rv.StationId == _station.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Second_review_by_same_user_updates_the_first()
    {
        var existing = new Review
        {
            Id = Guid.NewGuid(), StationId = _station.Id, UserId = Stranger,
            Rating = 2, Comment = "Broken", CreatedAt = DateTime.UtcNow.AddDays(-3),
        };
        _station.Reviews.Add(existing);

        var result = await _service.AddReviewAsync(_station.Id, new CreateReviewRequest(5, "Fixed now"), Stranger, CancellationToken.None);

        result.Should().Be(ReviewUpsertResult.Updated);
        existing.Rating.Should().Be(5);
        existing.Comment.Should().Be("Fixed now");
        existing.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        _stations.Verify(r => r.UpdateReviewAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _stations.Verify(r => r.AddReviewAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Review_by_another_user_is_added_separately()
    {
        _station.Reviews.Add(new Review { Id = Guid.NewGuid(), StationId = _station.Id, UserId = Owner, Rating = 3 });

        var result = await _service.AddReviewAsync(_station.Id, new CreateReviewRequest(5, null), Stranger, CancellationToken.None);

        result.Should().Be(ReviewUpsertResult.Created);
    }

    [Fact]
    public async Task Checkin_records_its_author()
    {
        await _service.AddCheckinAsync(_station.Id, new CheckinRequest("Working"), Stranger, CancellationToken.None);

        _stations.Verify(r => r.AddCheckinAsync(
            It.Is<AvailabilityCheckin>(c => c.UserId == Stranger && c.StationId == _station.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Review confirmation email: only for a NEW review, only when we know the email ---

    [Fact]
    public async Task New_review_queues_one_confirmation_in_the_users_language()
    {
        await _service.AddReviewAsync(_station.Id, new CreateReviewRequest(4, "Good"), Stranger, CancellationToken.None);

        _emailQueue.Verify(q => q.EnqueueAsync(
            It.Is<EmailMessage>(m => m.To == "stranger@example.com" && m.Subject.Contains("Original") && m.Html.Contains("lang=\"en\"")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Review_update_queues_no_email()
    {
        _station.Reviews.Add(new Review { Id = Guid.NewGuid(), StationId = _station.Id, UserId = Stranger, Rating = 2 });

        await _service.AddReviewAsync(_station.Id, new CreateReviewRequest(5, null), Stranger, CancellationToken.None);

        _emailQueue.Verify(q => q.EnqueueAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task User_without_email_gets_no_confirmation()
    {
        // Admin exists but has no Email; Owner has no AppUser row at all
        await _service.AddReviewAsync(_station.Id, new CreateReviewRequest(4, null), Admin, CancellationToken.None);
        var result = await _service.AddReviewAsync(_station.Id, new CreateReviewRequest(4, null), Owner, CancellationToken.None);

        result.Should().Be(ReviewUpsertResult.Created);
        _emailQueue.Verify(q => q.EnqueueAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Email_failure_does_not_fail_the_review()
    {
        _emailQueue.Setup(q => q.EnqueueAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue closed"));

        var result = await _service.AddReviewAsync(_station.Id, new CreateReviewRequest(4, null), Stranger, CancellationToken.None);

        result.Should().Be(ReviewUpsertResult.Created);
    }

    // --- Reviews list: author names, newest first ---

    [Fact]
    public async Task Reviews_are_newest_first_with_author_names_loaded_once()
    {
        var anonymous = Guid.NewGuid(); // no AppUser row
        _station.Reviews.AddRange([
            new Review { Id = Guid.NewGuid(), UserId = Stranger, Rating = 4, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new Review { Id = Guid.NewGuid(), UserId = Owner, Rating = 5, CreatedAt = DateTime.UtcNow },
            new Review { Id = Guid.NewGuid(), UserId = anonymous, Rating = 2, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new Review { Id = Guid.NewGuid(), UserId = null, Rating = 3, CreatedAt = DateTime.UtcNow.AddDays(-3) },
        ]);
        _users.Setup(r => r.GetDisplayNamesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string?> { [Stranger] = "Sami", [Owner] = "  " });

        var reviews = (await _service.GetReviewsAsync(_station.Id, CancellationToken.None))!;

        reviews.Select(r => r.Rating).Should().Equal(5, 2, 4, 3);
        reviews.Select(r => r.AuthorName).Should().Equal("Anonymous", "Anonymous", "Sami", "Anonymous");
        reviews.Select(r => r.UserId).Should().Equal(Owner, anonymous, Stranger, null);
        _users.Verify(r => r.GetDisplayNamesAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 3 && ids.Contains(Owner) && ids.Contains(Stranger) && ids.Contains(anonymous)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reviews_of_unknown_station_are_null()
    {
        (await _service.GetReviewsAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
    }
}
