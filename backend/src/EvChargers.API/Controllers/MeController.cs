using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;
using EvChargers.Domain.Entities;
using EvChargers.API.Extensions;

namespace EvChargers.API.Controllers;

[ApiController]
[Route("api/v1/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IUserRepository _users;
    public MeController(IUserRepository users) => _users = users;

    [HttpGet]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var user = await _users.GetByIdAsync(userId, ct);

        if (user is null)
        {
            // First login — upsert a minimal profile
            user = new AppUser { Id = userId };
            await _users.UpsertAsync(user, ct);
        }

        return Ok(new UserProfileDto(user.Id, user.DisplayName, user.AvatarUrl, user.IsAdmin, user.FavoriteStationIds));
    }

    [HttpPut("favorites/{stationId:guid}")]
    public async Task<IActionResult> AddFavorite(Guid stationId, CancellationToken ct)
    {
        await _users.AddFavoriteAsync(User.GetUserId(), stationId, ct);
        return NoContent();
    }

    [HttpDelete("favorites/{stationId:guid}")]
    public async Task<IActionResult> RemoveFavorite(Guid stationId, CancellationToken ct)
    {
        await _users.RemoveFavoriteAsync(User.GetUserId(), stationId, ct);
        return NoContent();
    }
}