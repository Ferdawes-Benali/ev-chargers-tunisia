using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using EvChargers.Application.Interfaces;
using EvChargers.Domain.Entities;
using EvChargers.Domain.Enums;

namespace EvChargers.Infrastructure.Persistence;

public class EfStationRepository : IStationRepository
{
    private readonly AppDbContext _db;
    public EfStationRepository(AppDbContext db) => _db = db;

    public async Task AddReviewAsync(Review review, CancellationToken ct)
    {
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddCheckinAsync(AvailabilityCheckin checkin, CancellationToken ct)
    {
        _db.Checkins.Add(checkin);
        await _db.SaveChangesAsync(ct);
    }
    public async Task<(List<Station> Items, int Total)> GetPagedAsync(
    int page, int size, string? connectorType, int? minPowerKw, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, 100);

        var query = _db.Stations.Include(s => s.Reviews).AsQueryable();

        if (!string.IsNullOrWhiteSpace(connectorType) &&
            Enum.TryParse<ConnectorType>(connectorType, true, out var type))
        {
            query = query.Where(s => s.Connectors.Any(c => c.Type == type));
        }

        if (minPowerKw.HasValue)
        {
            query = query.Where(s => s.Connectors.Any(c => c.PowerKw >= minPowerKw.Value));
        }

        query = query.OrderBy(s => s.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);
        return (items, total);
    }

    public Task<Station?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Stations
            .Include(s => s.Connectors)
            .Include(s => s.Reviews)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<List<Station>> GetNearbyAsync(double lat, double lng, double radiusKm, CancellationToken ct)
    {
        var origin = new Point(lng, lat) { SRID = 4326 };
        return await _db.Stations
            .Include(s => s.Reviews)
            .Where(s => s.Status == StationStatus.Verified
                    && s.Location.IsWithinDistance(origin, radiusKm * 1000))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Station station, CancellationToken ct)
    {
        _db.Stations.Add(station);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Station station, CancellationToken ct)
    {
        _db.Stations.Update(station);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var station = await _db.Stations.FindAsync([id], ct);
        if (station is not null)
        {
            _db.Stations.Remove(station);
            await _db.SaveChangesAsync(ct);
        }
    }
}