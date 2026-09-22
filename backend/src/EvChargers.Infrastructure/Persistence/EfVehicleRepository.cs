using Microsoft.EntityFrameworkCore;
using EvChargers.Application.Interfaces;
using EvChargers.Domain.Entities;

namespace EvChargers.Infrastructure.Persistence;

public class EfVehicleRepository : IVehicleRepository
{
    private readonly AppDbContext _db;
    public EfVehicleRepository(AppDbContext db) => _db = db;

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<List<Vehicle>> GetAllAsync(CancellationToken ct) =>
        _db.Vehicles.ToListAsync(ct);
}