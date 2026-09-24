using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;

namespace EvChargers.API.Controllers;

[ApiController]
[Route("api/v1/stations/{stationId:guid}/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IStationService _stations;
    private readonly IValidator<CreateReviewRequest> _validator;

    public ReviewsController(IStationService stations, IValidator<CreateReviewRequest> validator)
    {
        _stations = stations;
        _validator = validator;
    }

        [HttpGet]
    public async Task<IActionResult> List(Guid stationId, CancellationToken ct)
    {
        var reviews = await _stations.GetReviewsAsync(stationId, ct);
        return reviews is null ? NotFound() : Ok(reviews);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid stationId, [FromBody] CreateReviewRequest req, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(req, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));

        var success = await _stations.AddReviewAsync(stationId, req, ct);
        return success ? Created() : NotFound();
    }
}