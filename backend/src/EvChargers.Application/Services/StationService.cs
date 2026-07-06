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
    public StationService(IStationRepository stations) => _stations = stations;

    public async Task<List<StationListItemDto>> GetPagedAsync(int page, int size, CancellationToken ct)
    {
        var list = await _stations.GetPagedAsync(page, size, ct);
        return list.Select(s => s.ToListItemDto()).ToList();
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

    public async Task<Guid> CreateAsync(CreateStationRequest req, CancellationToken ct)
    {
        var station = new Station
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Address = req.Address,
            Location = new Point(req.Lng, req.Lat) { SRID = 4326 },
            OperatorId = req.OperatorId,
            Status = StationStatus.Pending,
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

    public async Task<bool> UpdateAsync(Guid id, CreateStationRequest req, CancellationToken ct)
    {
        var station = await _stations.GetByIdAsync(id, ct);
        if (station is null) return false;

        station.Name = req.Name;
        station.Address = req.Address;
        station.Location = new Point(req.Lng, req.Lat) { SRID = 4326 };
        station.OperatorId = req.OperatorId;

        await _stations.UpdateAsync(station, ct);
        return true;
    }

    public async Task<bool> VerifyAsync(Guid id, CancellationToken ct)
    {
        var station = await _stations.GetByIdAsync(id, ct);
        if (station is null) return false;

        station.Status = StationStatus.Verified;
        await _stations.UpdateAsync(station, ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var station = await _stations.GetByIdAsync(id, ct);
        if (station is null) return false;

        await _stations.DeleteAsync(id, ct);
        return true;
    }

    public async Task<List<ReviewDto>> GetReviewsAsync(Guid stationId, CancellationToken ct)
    {
        var station = await _stations.GetByIdAsync(stationId, ct);
        return station?.Reviews.Select(r => new ReviewDto(r.Id, r.Rating, r.Comment, r.CreatedAt)).ToList()
               ?? [];
    }

    public async Task AddReviewAsync(Guid stationId, CreateReviewRequest req, CancellationToken ct)
    {
        var review = new Review
        {
            Id = Guid.NewGuid(),
            StationId = stationId,
            Rating = req.Rating,
            Comment = req.Comment
        };
        await _stations.AddReviewAsync(review, ct);
    }

    public async Task AddCheckinAsync(Guid stationId, CheckinRequest req, CancellationToken ct)
    {
        var checkin = new AvailabilityCheckin
        {
            Id = Guid.NewGuid(),
            StationId = stationId,
            State = Enum.Parse<CheckinState>(req.State, ignoreCase: true)
        };
        await _stations.AddCheckinAsync(checkin, ct);
    }
}