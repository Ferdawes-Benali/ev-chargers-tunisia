using EvChargers.Application.Common;
using EvChargers.Application.DTOs;

namespace EvChargers.Application.Interfaces;

public interface IStationService
{
    Task<Common.PagedResult<StationListItemDto>> GetPagedAsync(int page, int size, string? connectorType, int? minPowerKw, CancellationToken ct);
    Task<StationDetailDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<StationListItemDto>> GetNearbyAsync(double lat, double lng, double radiusKm, CancellationToken ct);
    Task<Guid> CreateAsync(CreateStationRequest req, Guid userId, CancellationToken ct);
    /// <summary>Allowed for the user who submitted the station, or an admin.</summary>
    Task<OperationResult> UpdateAsync(Guid id, CreateStationRequest req, Guid userId, CancellationToken ct);
    /// <summary>Admin only; writes an audit log entry.</summary>
    Task<OperationResult> VerifyAsync(Guid id, Guid userId, CancellationToken ct);
    /// <summary>Admin only; writes an audit log entry.</summary>
    Task<OperationResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct);

    Task<List<ReviewDto>?> GetReviewsAsync(Guid stationId, CancellationToken ct);
    /// <summary>One review per user per station: a second review replaces the first.</summary>
    Task<ReviewUpsertResult> AddReviewAsync(Guid stationId, CreateReviewRequest req, Guid userId, CancellationToken ct);
    Task<bool> AddCheckinAsync(Guid stationId, CheckinRequest req, Guid userId, CancellationToken ct);
    Task<List<StationListItemDto>> GetByBoundingBoxAsync(double south, double west, double north, double east, CancellationToken ct);
}
