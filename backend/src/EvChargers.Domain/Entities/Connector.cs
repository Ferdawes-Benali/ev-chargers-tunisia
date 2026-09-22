using EvChargers.Domain.Enums;

namespace EvChargers.Domain.Entities;

public class Connector
{
    public Guid Id { get; set; }
    public Guid StationId { get; set; }
    public ConnectorType Type { get; set; }
    public int PowerKw { get; set; }
    public int Count { get; set; } = 1;
}