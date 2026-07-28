using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;

namespace EvChargers.API.Controllers;

[ApiController]
[Route("api/v1/trips")]
public class TripsController : ControllerBase
{
    private readonly ITripPlannerService _planner;
    private readonly IValidator<TripPlanRequest> _validator;

    public TripsController(ITripPlannerService planner, IValidator<TripPlanRequest> validator)
    {
        _planner = planner;
        _validator = validator;
    }

    [HttpPost("plan")]
    public async Task<IActionResult> Plan([FromBody] TripPlanRequest req, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(req, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));

        var outcome = await _planner.PlanAsync(req, ct);
        return outcome.Status switch
        {
            TripPlanStatus.VehicleNotFound => NotFound("Vehicle not found"),
            TripPlanStatus.RouteUnavailable => Problem(
                title: "Route unavailable, try again",
                detail: "We couldn't get a road route between these places right now.",
                statusCode: StatusCodes.Status502BadGateway),
            _ => Ok(outcome.Plan),
        };
    }
}
