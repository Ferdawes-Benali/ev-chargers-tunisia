using Microsoft.EntityFrameworkCore;
using EvChargers.Domain.Entities;

namespace EvChargers.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Vehicles.AnyAsync()) return; // already seeded

        db.Vehicles.AddRange(
            new Vehicle { Id = Guid.NewGuid(), Make = "Tesla", Model = "Model 3", BatteryKwh = 60, BaseConsumptionWhPerKm = 150, SocReservePercent = 5 },
            new Vehicle { Id = Guid.NewGuid(), Make = "Renault", Model = "Zoe", BatteryKwh = 52, BaseConsumptionWhPerKm = 170, SocReservePercent = 10 },
            new Vehicle { Id = Guid.NewGuid(), Make = "Nissan", Model = "Leaf", BatteryKwh = 40, BaseConsumptionWhPerKm = 180, SocReservePercent = 10 },
            new Vehicle { Id = Guid.NewGuid(), Make = "Hyundai", Model = "Kona Electric", BatteryKwh = 64, BaseConsumptionWhPerKm = 155, SocReservePercent = 8 },
            new Vehicle { Id = Guid.NewGuid(), Make = "Peugeot", Model = "e-208", BatteryKwh = 50, BaseConsumptionWhPerKm = 165, SocReservePercent = 10 }
        );

        await db.SaveChangesAsync();
    }
}