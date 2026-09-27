namespace EvChargers.Application.DTOs;

public record VehicleDto(Guid Id, string Name, double BatteryKwh, double SocReservePercent);
