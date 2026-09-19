using BookingEngine.Domain.Properties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingEngine.Infrastructure.Persistence.Configurations;

internal sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(200);
        builder.Property(p => p.Currency).HasMaxLength(3).IsFixedLength();
        builder.Property(p => p.Timezone).HasMaxLength(64);
        builder.Property(p => p.CountryCode).HasMaxLength(2).IsFixedLength();
        builder.Property(p => p.City).HasMaxLength(100);
        builder.Property(p => p.Address).HasMaxLength(500);
        builder.Property(p => p.Latitude).HasPrecision(9, 6);
        builder.Property(p => p.Longitude).HasPrecision(9, 6);
    }
}
