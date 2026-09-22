namespace EvChargers.Domain.Entities;

public class Review
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public Guid? UserId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}