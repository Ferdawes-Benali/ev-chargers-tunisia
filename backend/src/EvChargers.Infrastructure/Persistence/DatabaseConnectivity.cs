namespace EvChargers.Infrastructure.Persistence;

/// <summary>Whether PostgreSQL can be reached right now (used by the API's readiness check).</summary>
public sealed class DatabaseConnectivity(AppDbContext db)
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => db.Database.CanConnectAsync(cancellationToken);
}
