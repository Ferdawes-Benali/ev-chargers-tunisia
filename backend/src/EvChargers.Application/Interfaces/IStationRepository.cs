using EvChargers.Domain.Entities;

namespace EvChargers.Application.Interfaces;

public interface IStationRepository
{
    Task<(List<Station> Items, int Total)> GetPagedAsync(int page, int size, string? connectorType, int? minPowerKw, CancellationToken ct);    Task<Station?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<Station>> GetNearbyAsync(double lat, double lng, double radiusKm, CancellationToken ct);
    Task AddAsync(Station station, CancellationToken ct);
    Task UpdateAsync(Station station, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task AddReviewAsync(Review review, CancellationToken ct);
    Task AddCheckinAsync(AvailabilityCheckin checkin, CancellationToken ct);
    Task<List<Station>> GetByBoundingBoxAsync(double south, double west, double north, double east, CancellationToken ct);
}