using Microsoft.AspNetCore.Mvc;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;

namespace EvChargers.API.Controllers;

[ApiController]
[Route("api/v1/geocode")]
public class GeocodeController : ControllerBase
{
    private readonly IGeocodingProvider _geocoding;
    public GeocodeController(IGeocodingProvider geocoding) => _geocoding = geocoding;

    [HttpGet("autocomplete")]
    public async Task<IActionResult> Autocomplete([FromQuery] string? q, [FromQuery] double? lat, [FromQuery] double? lng, CancellationToken ct)
    {
        var query = q?.Trim() ?? "";
        if (query.Length < 2 || query.Length > 100)
            return BadRequest("Type between 2 and 100 characters to search for a place.");

        var suggestions = await _geocoding.AutocompleteAsync(query, lat, lng, ct);
        return Ok(suggestions ?? new List<PlaceSuggestionDto>());
    }
}
