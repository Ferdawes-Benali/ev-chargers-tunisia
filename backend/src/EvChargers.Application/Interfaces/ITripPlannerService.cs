using EvChargers.Application.DTOs;

namespace EvChargers.Application.Interfaces;

public interface ITripPlannerService
{
    Task<TripPlanOutcome> PlanAsync(TripPlanRequest request, CancellationToken ct);
}
