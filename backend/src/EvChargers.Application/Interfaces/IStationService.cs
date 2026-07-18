using EvChargers.Application.Common;
using EvChargers.Application.DTOs;

namespace EvChargers.Application.Interfaces;

public interface IStationService
{
    Task<Common.PagedResult<StationListItemDto>> GetPagedAsync(int page, int size, string? connectorType, int? minPowerKw, CancellationToken ct);
    Task<StationDetailDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<StationListItemDto>> GetNearbyAsync(double lat, double lng, double radiusKm, CancellationToken ct);
    Task<Guid> CreateAsync(CreateStationRequest req, CancellationToken ct);
    Task<bool> UpdateAsync(Guid id, CreateStationRequest req, CancellationToken ct);
    Task<bool> VerifyAsync(Guid id, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);

    Task<List<ReviewDto>> GetReviewsAsync(Guid stationId, CancellationToken ct);
    Task AddReviewAsync(Guid stationId, CreateReviewRequest req, CancellationToken ct);
    Task AddCheckinAsync(Guid stationId, CheckinRequest req, CancellationToken ct);
}