using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EvChargers.Application.Email;

namespace EvChargers.Infrastructure.Email;

public class EmailDispatcher : BackgroundService
{
    private const int MaxAttempts = 3;

    private readonly ChannelEmailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailDispatcher> _logger;

    public TimeSpan BackoffUnit { get; init; } = TimeSpan.FromSeconds(2);

    public EmailDispatcher(ChannelEmailQueue queue, IServiceScopeFactory scopeFactory, ILogger<EmailDispatcher> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                // Scope sender dependencies to a single email.
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

                var result = await sender.SendAsync(message, stoppingToken);
                if (result == EmailSendResult.Sent)
                {
                    _logger.LogInformation("Email sent to {To}: {Subject}", Mask(message.To), message.Subject);
                    break;
                }

                if (result == EmailSendResult.PermanentFailure)
                {
                    _logger.LogError("Email to {To} rejected permanently, not retrying: {Subject}", Mask(message.To), message.Subject);
                    break;
                }

                if (attempt == MaxAttempts)
                {
                    _logger.LogError("Email to {To} failed after {Attempts} attempts: {Subject}", Mask(message.To), MaxAttempts, message.Subject);
                }
                else
                {
                    await Task.Delay(BackoffUnit * attempt, stoppingToken);
                }
            }
        }
    }

    private static string Mask(string email) => EmailMasking.Mask(email);
}
