using EvChargers.Application.Common;
using NetTopologySuite.Geometries;
using EvChargers.Application.Common.Mapping;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;
using EvChargers.Domain.Entities;
using EvChargers.Domain.Enums;

namespace EvChargers.Application.Services;

public class StationService : IStationService
{
    private readonly IStationRepository _stations;
    private readonly IUserRepository _users;
    private readonly IAuditLogRepository _auditLog;

    public StationService(IStationRepository stations, IUserRepository users, IAuditLogRepository auditLog)
    {
        _stations = stations;
        _users = users;
        _auditLog = auditLog;
    }

    public async Task<PagedResult<StationListItemDto>> GetPagedAsync(int page, int size, string? connectorType, int? minPowerKw, CancellationToken ct)
    {
        var (items, total) = await _stations.GetPagedAsync(page, size, connectorType, minPowerKw, ct);
        var dtos = items.Select(s => s.ToListItemDto()).ToList();
        return new PagedResult<StationListItemDto>(dtos, page, size, total);
    }

    public async Task<StationDetailDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var station = await _stations.GetByIdAsync(id, ct);
        return station?.ToDetailDto();
    }

    public async Task<List<StationListItemDto>> GetNearbyAsync(double lat, double lng, double radiusKm, CancellationToken ct)
    {
        var list = await _stations.GetNearbyAsync(lat, lng, radiusKm, ct);
        return list.Select(s => s.ToListItemDto()).ToList();
    }

    public async Task<Guid> CreateAsync(CreateStationRequest req, Guid userId, CancellationToken ct)
    {
        var station = new Station
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Address = req.Address,
            Location = new Point(req.Lng, req.Lat) { SRID = 4326 },
            OperatorId = req.OperatorId,
            Status = StationStatus.Pending,
            SubmittedBy = userId,
            Connectors = req.Connectors.Select(c => new Connector
            {
                Id = Guid.NewGuid(),
                Type = Enum.Parse<ConnectorType>(c.Type),
                PowerKw = c.PowerKw,
                Count = c.Count
            }).ToList()
        };
        await _stations.AddAsync(station, ct);
        return station.Id;
    }

    public async Task<OperationResult> UpdateAsync(Guid id, CreateStationRequest req, Guid userId, CancellationToken ct)
    {
        var station = await _stations.GetByIdAsync(id, ct);
        if (station is null) return OperationResult.NotFound;

        // Only the submitter or an admin may edit. Stations without a submitter are admin-only.
        if (station.SubmittedBy != userId && !await IsAdminAsync(userId, ct))
            return OperationResult.Forbidden;

        station.Name = req.Name;
        station.Address = req.Address;
        station.Location = new Point(req.Lng, req.Lat) { SRID = 4326 };
        station.OperatorId = req.OperatorId;

        await _stations.UpdateAsync(station, ct);
        return OperationResult.Ok;
    }

    public async Task<OperationResult> VerifyAsync(Guid id, Guid userId, CancellationToken ct)
    {
        // Admin check first, so non-admins can't probe which station ids exist
        if (!await IsAdminAsync(userId, ct)) return OperationResult.Forbidden;

        var station = await _stations.GetByIdAsync(id, ct);
        if (station is null) return OperationResult.NotFound;

        station.Status = StationStatus.Verified;
        await _stations.UpdateAsync(station, ct);
        await LogAdminActionAsync(userId, "VerifyStation", id, ct);
        return OperationResult.Ok;
    }

    public async Task<OperationResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        if (!await IsAdminAsync(userId, ct)) return OperationResult.Forbidden;

        var station = await _stations.GetByIdAsync(id, ct);
        if (station is null) return OperationResult.NotFound;

        await _stations.DeleteAsync(id, ct);
        await LogAdminActionAsync(userId, "DeleteStation", id, ct);
        return OperationResult.Ok;
    }

    public async Task<List<ReviewDto>?> GetReviewsAsync(Guid stationId, CancellationToken ct)
    {
        var station = await _stations.GetByIdAsync(stationId, ct);
        return station?.Reviews.Select(r => new ReviewDto(r.Id, r.Rating, r.Comment, r.CreatedAt)).ToList();
    }

    public async Task<ReviewUpsertResult> AddReviewAsync(Guid stationId, CreateReviewRequest req, Guid userId, CancellationToken ct)
    {
        var station = await _stations.GetByIdAsync(stationId, ct);
        if (station is null) return ReviewUpsertResult.StationNotFound;

        // One review per user per station: a second review replaces the first
        var existing = station.Reviews.FirstOrDefault(r => r.UserId == userId);
        if (existing is not null)
        {
            existing.Rating = req.Rating;
            existing.Comment = req.Comment;
            existing.CreatedAt = DateTime.UtcNow;
            await _stations.UpdateReviewAsync(existing, ct);
            return ReviewUpsertResult.Updated;
        }

        var review = new Review
        {
            Id = Guid.NewGuid(),
            StationId = stationId,
            UserId = userId,
            Rating = req.Rating,
            Comment = req.Comment
        };
        await _stations.AddReviewAsync(review, ct);
        return ReviewUpsertResult.Created;
    }

    public async Task<bool> AddCheckinAsync(Guid stationId, CheckinRequest req, Guid userId, CancellationToken ct)
    {
        var station = await _stations.GetByIdAsync(stationId, ct);
        if (station is null) return false;

        var checkin = new AvailabilityCheckin
        {
            Id = Guid.NewGuid(),
            StationId = stationId,
            UserId = userId,
            State = Enum.Parse<CheckinState>(req.State, ignoreCase: true)
        };
        await _stations.AddCheckinAsync(checkin, ct);
        return true;
    }

    private async Task<bool> IsAdminAsync(Guid userId, CancellationToken ct) =>
        (await _users.GetByIdAsync(userId, ct))?.IsAdmin == true;

    private Task LogAdminActionAsync(Guid userId, string action, Guid targetId, CancellationToken ct) =>
        _auditLog.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            TargetId = targetId
        }, ct);

    public async Task<List<StationListItemDto>> GetByBoundingBoxAsync(double south, double west, double north, double east, CancellationToken ct)
    {
        var list = await _stations.GetByBoundingBoxAsync(south, west, north, east, ct);
        return list.Select(s => s.ToListItemDto()).ToList();    
    }
}