using Microsoft.AspNetCore.Mvc;
using EvChargers.Application.Interfaces;

namespace EvChargers.API.Controllers;

[ApiController]
[Route("api/v1/vehicles")]
public class VehiclesController : ControllerBase
{
    private readonly IVehicleRepository _vehicles;
    public VehiclesController(IVehicleRepository vehicles) => _vehicles = vehicles;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var list = await _vehicles.GetAllAsync(ct);
        return Ok(list.Select(v => new { v.Id, Name = $"{v.Make} {v.Model}", v.BatteryKwh }));
    }
}