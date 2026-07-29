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