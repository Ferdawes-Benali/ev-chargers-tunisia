using Microsoft.AspNetCore.Mvc;
using EvChargers.Application.Interfaces;

namespace EvChargers.API.Controllers;

[ApiController]
[Route("api/v1/stations/{id:guid}/companion")]
public class CompanionController : ControllerBase
{
    private readonly ICompanionService _companion;
    public CompanionController(ICompanionService companion) => _companion = companion;

    /// <summary>Places within walking distance while charging. 200 with "unavailable": true if OpenStreetMap can't be reached.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await _companion.GetAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Walking route to one place. 200 with "estimated": true and no points when routing is unavailable.</summary>
    [HttpGet("route")]
    public async Task<IActionResult> Route(Guid id, [FromQuery] string? placeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(placeId)) return BadRequest("placeId is required");

        var route = await _companion.GetWalkingRouteAsync(id, placeId, ct);
        return route is null ? NotFound() : Ok(route);
    }
}
