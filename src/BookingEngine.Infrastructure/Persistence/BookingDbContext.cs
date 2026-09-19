using BookingEngine.Domain.Inventory;
using BookingEngine.Domain.Properties;
using BookingEngine.Domain.RatePlans;
using BookingEngine.Domain.RoomTypes;
using Microsoft.EntityFrameworkCore;

namespace BookingEngine.Infrastructure.Persistence;

public sealed class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<RatePlan> RatePlans => Set<RatePlan>();
    public DbSet<RoomTypeAvailability> Availability => Set<RoomTypeAvailability>();
    public DbSet<RatePlanRestriction> Restrictions => Set<RatePlanRestriction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
}
