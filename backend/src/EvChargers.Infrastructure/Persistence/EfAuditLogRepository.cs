using EvChargers.Application.Interfaces;
using EvChargers.Domain.Entities;

namespace EvChargers.Infrastructure.Persistence;

public class EfAuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _db;
    public EfAuditLogRepository(AppDbContext db) => _db = db;

    public async Task LogAsync(AuditLog entry, CancellationToken ct)
    {
        _db.AuditLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
    }
}