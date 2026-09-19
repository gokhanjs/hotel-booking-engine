using BookingEngine.Domain.Inventory;
using BookingEngine.Domain.RoomTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingEngine.Infrastructure.Persistence.Configurations;

internal sealed class RoomTypeAvailabilityConfiguration : IEntityTypeConfiguration<RoomTypeAvailability>
{
    public void Configure(EntityTypeBuilder<RoomTypeAvailability> builder)
    {
        builder.ToTable("room_type_availability", t =>
            t.HasCheckConstraint("ck_room_type_availability_availability", "availability >= 0"));

        builder.HasKey(a => new { a.RoomTypeId, a.Date });
        builder.HasOne<RoomType>().WithMany().HasForeignKey(a => a.RoomTypeId).OnDelete(DeleteBehavior.Cascade);
    }
}
