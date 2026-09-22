namespace EvChargers.Domain.Entities;

public class Operator
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public List<Station> Stations { get; set; } = [];
}