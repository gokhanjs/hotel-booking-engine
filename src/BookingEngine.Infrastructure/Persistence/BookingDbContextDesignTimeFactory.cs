using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookingEngine.Infrastructure.Persistence;

// Used by `dotnet ef` and the migration bundle; the bundle reads the same variable the API uses in containers.
internal sealed class BookingDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<BookingDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Postgres") ?? "Host=localhost;Port=5433;Database=booking;Username=booking")
        .UseSnakeCaseNamingConvention()
        .Options);
}
