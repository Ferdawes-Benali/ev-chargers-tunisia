using Microsoft.EntityFrameworkCore;
using EvChargers.Domain.Entities;

namespace EvChargers.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Connector> Connectors => Set<Connector>();
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<AvailabilityCheckin> Checkins => Set<AvailabilityCheckin>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}