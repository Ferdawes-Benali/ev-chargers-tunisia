using EvChargers.Application.DTOs;

namespace EvChargers.Application.Interfaces;

public interface IReachEstimatorService
{
    Task<ReachEstimateResult?> EstimateAsync(ReachEstimateRequest request, CancellationToken ct);
}