using Microsoft.AspNetCore.Mvc;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;

namespace EvChargers.API.Controllers;

[ApiController]
[Route("api/v1/stations")]
public class StationsController : ControllerBase
{
    private readonly IStationService _stations;
    public StationsController(IStationService stations) => _stations = stations;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
        => Ok(await _stations.GetPagedAsync(page, size, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _stations.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("nearby")]
    public async Task<IActionResult> Nearby([FromQuery] double lat, [FromQuery] double lng,
                                            [FromQuery] double radiusKm = 10, CancellationToken ct = default)
        => Ok(await _stations.GetNearbyAsync(lat, lng, radiusKm, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStationRequest req, CancellationToken ct)
    {
        var id = await _stations.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateStationRequest req, CancellationToken ct)
    {
        var success = await _stations.UpdateAsync(id, req, ct);
        return success ? NoContent() : NotFound();
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, CancellationToken ct)
    {
        var success = await _stations.VerifyAsync(id, ct);
        return success ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var success = await _stations.DeleteAsync(id, ct);
        return success ? NoContent() : NotFound();
    }
}