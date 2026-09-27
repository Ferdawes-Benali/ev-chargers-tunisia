namespace EvChargers.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; } = default!;
    public Guid? TargetId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}