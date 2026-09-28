using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FluentValidation;
using EvChargers.Application.DTOs;
using EvChargers.Application.Interfaces;
using EvChargers.API.Extensions;

namespace EvChargers.API.Controllers;

[ApiController]
[Route("api/v1/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IUserRepository _users;
    private readonly IValidator<UpdateLanguageRequest> _languageValidator;

    public MeController(IUserService userService, IUserRepository users, IValidator<UpdateLanguageRequest> languageValidator)
    {
        _userService = userService;
        _users = users;
        _languageValidator = languageValidator;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var (displayName, avatarUrl) = User.GetProfileFromMetadata();
        var language = AcceptLanguage.PickSupported(Request.Headers.AcceptLanguage.ToString());

        var (profile, _) = await _userService.EnsureUserAsync(
            User.GetUserId(), User.GetEmail(), displayName, avatarUrl, language, ct);
        return Ok(profile);
    }

    [HttpPut("language")]
    public async Task<IActionResult> SetLanguage([FromBody] UpdateLanguageRequest req, CancellationToken ct)
    {
        var validation = await _languageValidator.ValidateAsync(req, ct);
        if (!validation.IsValid)
            return BadRequest(validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));

        return await _userService.SetLanguageAsync(User.GetUserId(), req.Language!, ct)
            ? NoContent()
            : NotFound();
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
