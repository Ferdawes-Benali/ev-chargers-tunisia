using EvChargers.Application.DTOs;

namespace EvChargers.Application.Interfaces;

public interface ICompanionService
{
    /// <summary>Places worth walking to while charging at this station; null if the station does not exist.</summary>
    Task<CompanionResultDto?> GetAsync(Guid stationId, CancellationToken ct);

    /// <summary>
    /// Walking route from the station to one of its companion places; null if the station or place is unknown.
    /// Falls back to a straight-line estimate (Estimated = true) when routing is unavailable.
    /// </summary>
    Task<WalkingRouteDto?> GetWalkingRouteAsync(Guid stationId, string placeId, CancellationToken ct);
}
