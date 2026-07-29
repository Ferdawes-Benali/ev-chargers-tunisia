using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EvChargers.Application.Email;

namespace EvChargers.Infrastructure.Email;

/// <summary>
/// Runs for the whole life of the API. Takes emails from the queue one by one and sends them,
/// retrying a few times on failure. A failed email never crashes the loop.
/// </summary>
public class EmailDispatcher : BackgroundService
{
    private const int MaxAttempts = 3;

    private readonly ChannelEmailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailDispatcher> _logger;

    public EmailDispatcher(ChannelEmailQueue queue, IServiceScopeFactory scopeFactory, ILogger<EmailDispatcher> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Waits (without using CPU) until an email arrives, forever, until the app stops.
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                // A fresh scope per email: the sender's HttpClient must not live forever inside this singleton.
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                if (await sender.SendAsync(message, stoppingToken))
                {
                    _logger.LogInformation("Email sent to {To}: {Subject}", Mask(message.To), message.Subject);
                    break;
                }

                if (attempt == MaxAttempts)
                {
                    _logger.LogError("Email to {To} failed after {Attempts} attempts: {Subject}", Mask(message.To), MaxAttempts, message.Subject);
                }
                else
                {
                    // Back off a little more each time: 2 s, then 4 s
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), stoppingToken);
                }
            }
        }
    }

    /// <summary>"ferdawes@gmail.com" → "f***@gmail.com". Email addresses are personal data: don't write them in logs.</summary>
    private static string Mask(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : $"{email[0]}***{email[at..]}";
    }
}