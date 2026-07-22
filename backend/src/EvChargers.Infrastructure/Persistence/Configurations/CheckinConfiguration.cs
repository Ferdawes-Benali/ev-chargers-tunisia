using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EvChargers.Domain.Entities;

namespace EvChargers.Infrastructure.Persistence.Configurations;

public class CheckinConfiguration : IEntityTypeConfiguration<AvailabilityCheckin>
{
    public void Configure(EntityTypeBuilder<AvailabilityCheckin> e)
    {
        e.HasOne<Station>()
            .WithMany()
            .HasForeignKey(c => c.StationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}