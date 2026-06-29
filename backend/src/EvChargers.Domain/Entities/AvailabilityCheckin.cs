using EvChargers.Domain.Enums;

namespace EvChargers.Domain.Entities;

public class AvailabilityCheckin
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public Guid? UserId { get; set; }
    public CheckinState State { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}