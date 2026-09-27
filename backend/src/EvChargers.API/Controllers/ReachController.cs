using Microsoft.AspNetCore.Mvc;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;

namespace EvChargers.API.Controllers;

[ApiController]
[Route("api/v1/reach")]
public class ReachController : ControllerBase
{
    private readonly IReachEstimatorService _reach;
    public ReachController(IReachEstimatorService reach) => _reach = reach;

    [HttpPost("estimate")]
    public async Task<IActionResult> Estimate([FromBody] ReachEstimateRequest req, CancellationToken ct)
    {
        var result = await _reach.EstimateAsync(req, ct);
        return result is null ? NotFound("Vehicle not found") : Ok(result);
    }
}