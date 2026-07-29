using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EvChargers.Domain.Entities;

namespace EvChargers.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> e)
    {
        e.Property(u => u.FavoriteStationIds).HasColumnType("uuid[]");
        e.Property(u => u.Email).HasMaxLength(320);
        // Default also fills the column for users that existed before the migration
        e.Property(u => u.PreferredLanguage).HasMaxLength(2).HasDefaultValue("fr");
    }
}
