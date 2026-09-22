using NetTopologySuite.Geometries;
using EvChargers.Domain.Enums;

namespace EvChargers.Domain.Entities;

public class Station
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Address { get; set; }
    public Point Location { get; set; } = default!;
    public Guid? OperatorId { get; set; }
    public Operator? Operator { get; set; }
    public StationStatus Status { get; set; } = StationStatus.Pending;
    public string CountryCode { get; set; } = "TN";
    public Guid? SubmittedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Connector> Connectors { get; set; } = [];
    public List<Review> Reviews { get; set; } = [];
}