using EvChargers.Domain.Entities;

namespace EvChargers.Application.Interfaces;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<List<Vehicle>> GetAllAsync(CancellationToken ct);
}