using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EvChargers.Infrastructure.Email;

/// <summary>At startup, warns when the Development-only redirect is configured in another environment.</summary>
public class EmailConfigurationCheck : IHostedService
{
    private readonly EmailOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<EmailConfigurationCheck> _logger;

    public EmailConfigurationCheck(IOptions<EmailOptions> options, IHostEnvironment environment, ILogger<EmailConfigurationCheck> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() && !string.IsNullOrWhiteSpace(_options.DevRedirectTo))
        {
            _logger.LogWarning(
                "Email:DevRedirectTo is set outside Development (environment {Environment}) and is ignored: emails go to their real recipients. Remove the setting from this host.",
                _environment.EnvironmentName);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
