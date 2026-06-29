namespace EvChargers.Domain.Entities;

public class Vehicle
{
    public Guid Id { get; set; }
    public string Make { get; set; } = default!;
    public string Model { get; set; } = default!;
    public double BatteryKwh { get; set; }
    public double BaseConsumptionWhPerKm { get; set; }
    public double SocReservePercent { get; set; } = 10;
}