using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;
using EvChargers.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace EvChargers.API.Controllers;

[ApiController]
[Route("api/v1/stations")]
public class StationsController : ControllerBase
{
    private readonly IStationService _stations;
    private readonly IValidator<CreateStationRequest> _validator;
    private readonly IValidator<CheckinRequest> _checkinValidator;
    private readonly IUserRepository _users;
    private readonly IAuditLogRepository _auditLog;

    public StationsController(
        IStationService stations,
        IValidator<CreateStationRequest> validator,
        IValidator<CheckinRequest> checkinValidator,
        IUserRepository users,
        IAuditLogRepository auditLog)
    {
        _stations = stations;
        _validator = validator;
        _checkinValidator = checkinValidator;
        _users = users;
        _auditLog = auditLog;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("No user id claim found"));

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int size = 20,
        [FromQuery] string? connectorType = null, [FromQuery] int? minPowerKw = null,
        CancellationToken ct = default)
        => Ok(await _stations.GetPagedAsync(page, size, connectorType, minPowerKw, ct));

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
    
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStationRequest req, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(req, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));

        var id = await _stations.CreateAsync(req, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateStationRequest req, CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(req, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));

        var success = await _stations.UpdateAsync(id, req, ct);
        return success ? NoContent() : NotFound();
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = CurrentUserId;
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsAdmin)
            return Forbid();

        var success = await _stations.DeleteAsync(id, ct);
        if (!success) return NotFound();

        await _auditLog.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "DeleteStation",
            TargetId = id
        }, ct);

        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, CancellationToken ct)
    {
        var userId = CurrentUserId;
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || !user.IsAdmin)
            return Forbid();

        var success = await _stations.VerifyAsync(id, ct);
        if (!success) return NotFound();

        await _auditLog.LogAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "VerifyStation",
            TargetId = id
        }, ct);

        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/checkin")]
    public async Task<IActionResult> Checkin(Guid id, [FromBody] CheckinRequest req, CancellationToken ct)
    {
        var validation = await _checkinValidator.ValidateAsync(req, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));

        var success = await _stations.AddCheckinAsync(id, req, ct);
        return success ? Created() : NotFound();
    }

    [HttpGet("bbox")]
    public async Task<IActionResult> Bbox(
        [FromQuery] double south, [FromQuery] double west,
        [FromQuery] double north, [FromQuery] double east,
        CancellationToken ct)
        => Ok(await _stations.GetByBoundingBoxAsync(south, west, north, east, ct));
}