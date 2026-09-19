using BookingEngine.Domain.Inventory;
using BookingEngine.Domain.Properties;
using BookingEngine.Domain.RatePlans;
using BookingEngine.Domain.RoomTypes;
using Microsoft.EntityFrameworkCore;

namespace BookingEngine.Application.Abstractions;

public interface IBookingDbContext
{
    DbSet<Property> Properties { get; }
    DbSet<RoomType> RoomTypes { get; }
    DbSet<RatePlan> RatePlans { get; }
    DbSet<RoomTypeAvailability> Availability { get; }
    DbSet<RatePlanRestriction> Restrictions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
