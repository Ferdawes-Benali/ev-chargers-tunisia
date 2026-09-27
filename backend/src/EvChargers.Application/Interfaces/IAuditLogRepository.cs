using EvChargers.Domain.Entities;

namespace EvChargers.Application.Interfaces;

public interface IAuditLogRepository
{
    Task LogAsync(AuditLog entry, CancellationToken ct);
}