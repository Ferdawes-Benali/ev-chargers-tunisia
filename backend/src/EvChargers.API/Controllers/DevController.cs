using Microsoft.AspNetCore.Mvc;
using EvChargers.Application.Email;

namespace EvChargers.API.Controllers;


[ApiController]
[Route("api/v1/dev")]
public class DevController : ControllerBase
{
    private readonly IEmailQueue _emailQueue;
    private readonly IWebHostEnvironment _env;

    public DevController(IEmailQueue emailQueue, IWebHostEnvironment env)
    {
        _emailQueue = emailQueue;
        _env = env;
    }

    [HttpPost("preview-email")]
    public async Task<IActionResult> PreviewEmail([FromQuery] string to, [FromQuery] string template = "welcome",
        [FromQuery] string language = "fr", CancellationToken ct = default)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var message = template == "review"
            ? EmailTemplates.ReviewConfirmation(to, "Ferdawes", "Tunis City Center Charger", 4, language)
            : EmailTemplates.Welcome(to, "Ferdawes", language);

        await _emailQueue.EnqueueAsync(message, ct);
        return Accepted(new { queued = true, template, language });
    }

    [HttpPost("test-email")]
    public async Task<IActionResult> TestEmail([FromQuery] string to, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();

        await _emailQueue.EnqueueAsync(new EmailMessage(
            to,
            "Test from EV Chargers Tunisia ⚡",
            "<h1>It works! ⚡</h1><p>This email went through the queue and the background dispatcher.</p>"
        ), ct);

        return Accepted(new { queued = true });
    }
}