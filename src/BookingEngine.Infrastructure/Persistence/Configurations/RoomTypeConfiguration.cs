using BookingEngine.Domain.Properties;
using BookingEngine.Domain.RoomTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingEngine.Infrastructure.Persistence.Configurations;

internal sealed class RoomTypeConfiguration : IEntityTypeConfiguration<RoomType>
{
    public void Configure(EntityTypeBuilder<RoomType> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_room_types_count_of_rooms", "count_of_rooms >= 1");
            t.HasCheckConstraint("ck_room_types_max_adults", "max_adults >= 1");
            t.HasCheckConstraint("ck_room_types_max_children", "max_children >= 0");
        });

        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Code).HasMaxLength(50);
        builder.Property(r => r.Name).HasMaxLength(200);

        builder.HasOne<Property>().WithMany().HasForeignKey(r => r.PropertyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.PropertyId, r.Code }).IsUnique();
    }
}
