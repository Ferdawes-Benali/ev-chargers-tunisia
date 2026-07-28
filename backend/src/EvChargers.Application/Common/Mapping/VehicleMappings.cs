using EvChargers.Domain.Entities;
using EvChargers.Application.DTOs;

namespace EvChargers.Application.Common.Mapping;

public static class VehicleMappings
{
    public static VehicleDto ToDto(this Vehicle v) =>
        new(v.Id, $"{v.Make} {v.Model}", v.BatteryKwh, v.SocReservePercent);
}
