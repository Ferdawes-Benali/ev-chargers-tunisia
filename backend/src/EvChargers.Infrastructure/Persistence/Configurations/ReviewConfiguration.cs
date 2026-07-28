using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EvChargers.Domain.Entities;

namespace EvChargers.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> e)
    {
        // One review per user per station. Postgres treats NULLs as distinct,
        // so older anonymous reviews (UserId = null) don't conflict.
        e.HasIndex(r => new { r.StationId, r.UserId }).IsUnique();
    }
}
