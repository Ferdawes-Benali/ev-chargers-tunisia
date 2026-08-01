using Microsoft.Extensions.Diagnostics.HealthChecks;
using EvChargers.Infrastructure.Persistence;

namespace EvChargers.API.Health;

/// <summary>Readiness: the API is only useful when it can reach PostgreSQL.</summary>
public sealed class DatabaseHealthCheck(DatabaseConnectivity database) : IHealthCheck
{
    public const string ReadyTag = "ready";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database unreachable");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // No details in the response (see the response writer); the reason goes to the logs only
            return HealthCheckResult.Unhealthy("Database unreachable", ex);
        }
    }
}
