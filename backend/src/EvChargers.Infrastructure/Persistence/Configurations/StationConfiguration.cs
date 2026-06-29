using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EvChargers.Domain.Entities;

namespace EvChargers.Infrastructure.Persistence.Configurations;

public class StationConfiguration : IEntityTypeConfiguration<Station>
{
    public void Configure(EntityTypeBuilder<Station> e)
    {
        e.Property(s => s.Location).HasColumnType("geography (Point, 4326)");
        e.HasIndex(s => s.Location);
        e.HasIndex(s => s.Status);
        e.HasMany(s => s.Connectors).WithOne().HasForeignKey(c => c.StationId);
    }
}