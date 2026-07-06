using Microsoft.AspNetCore.Mvc;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;

namespace EvChargers.API.Controllers;

[ApiController]
[Route("api/v1/stations/{stationId:guid}/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IStationService _stations;
    public ReviewsController(IStationService stations) => _stations = stations;

    [HttpGet]
    public async Task<IActionResult> List(Guid stationId, CancellationToken ct)
        => Ok(await _stations.GetReviewsAsync(stationId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(Guid stationId, [FromBody] CreateReviewRequest req, CancellationToken ct)
    {
        await _stations.AddReviewAsync(stationId, req, ct);
        return Created();
    }
    [HttpPost("{id:guid}/checkin")]
    public async Task<IActionResult> Checkin(Guid id, [FromBody] CheckinRequest req, CancellationToken ct)
    {
        await _stations.AddCheckinAsync(id, req, ct);
        return Created();
    }
}