using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EvChargers.Domain.Entities;

namespace EvChargers.Infrastructure.Persistence.Configurations;

public class PlacesCacheConfiguration : IEntityTypeConfiguration<PlacesCacheEntry>
{
    public void Configure(EntityTypeBuilder<PlacesCacheEntry> e)
    {
        e.ToTable("PlacesCache");
        e.HasIndex(p => p.StationId).IsUnique();
        e.HasOne<Station>()
            .WithOne()
            .HasForeignKey<PlacesCacheEntry>(p => p.StationId)
            .OnDelete(DeleteBehavior.Cascade);
        e.Property(p => p.PlacesJson).HasColumnType("jsonb");
        e.Property(p => p.LastError).HasMaxLength(PlacesCacheEntry.MaxErrorLength);    }
}
