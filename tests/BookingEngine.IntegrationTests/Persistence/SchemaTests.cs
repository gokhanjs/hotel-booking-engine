using BookingEngine.Domain.Inventory;
using BookingEngine.Domain.Properties;
using BookingEngine.Domain.RatePlans;
using BookingEngine.Domain.RoomTypes;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookingEngine.IntegrationTests.Persistence;

public class SchemaTests(PostgresFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_are_in_sync_with_the_model()
    {
        await using var db = fixture.CreateDbContext();

        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
    }

    [Fact]
    public async Task Rate_plans_round_trip_with_occupancies_derivation_and_audit_fields()
    {
        var (roomType, parent) = await SeedAsync();
        var child = new RatePlan(roomType.Id, "NRF", "Non-refundable", SellMode.PerPerson, MealPlan.Breakfast, 20);
        child.DeriveFrom(parent, DerivedAdjustmentType.Percent, -10);

        await using (var db = fixture.CreateDbContext())
        {
            db.RatePlans.Add(child);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = fixture.CreateDbContext();
        var loadedParent = await read.RatePlans.SingleAsync(r => r.Id == parent.Id, Ct);
        var loadedChild = await read.RatePlans.SingleAsync(r => r.Id == child.Id, Ct);

        Assert.Equal([1, 2], loadedParent.Occupancies.Select(o => o.Adults));
        Assert.Equal(-20, loadedParent.Occupancies[0].PriceAdjustment);
        Assert.Equal(new DerivedPricing(parent.Id, DerivedAdjustmentType.Percent, -10), loadedChild.Derived);
        Assert.NotEqual(default, loadedChild.CreatedAt);
    }

    [Fact]
    public async Task Negative_availability_is_rejected_by_the_database()
    {
        var (roomType, _) = await SeedAsync();
        await using var db = fixture.CreateDbContext();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"INSERT INTO room_type_availability (room_type_id, date, availability) VALUES ({roomType.Id}, {new DateOnly(2026, 10, 1)}, -1)",
            Ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task Parent_rate_plan_with_derived_children_cannot_be_deleted()
    {
        var (roomType, parent) = await SeedAsync();
        var child = new RatePlan(roomType.Id, "NRF", "Non-refundable", SellMode.PerPerson, MealPlan.Breakfast, 20);
        child.DeriveFrom(parent, DerivedAdjustmentType.Amount, -15);

        await using var db = fixture.CreateDbContext();
        db.RatePlans.Add(child);
        await db.SaveChangesAsync(Ct);

        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            db.RatePlans.Where(r => r.Id == parent.Id).ExecuteDeleteAsync(Ct));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, ex.SqlState);
    }

    [Fact]
    public async Task Deleting_a_property_cascades_to_its_inventory()
    {
        var (roomType, parent) = await SeedAsync();
        await using var db = fixture.CreateDbContext();
        db.Availability.Add(new RoomTypeAvailability(roomType.Id, new DateOnly(2026, 10, 1), 5));
        db.Restrictions.Add(new RatePlanRestriction(parent.Id, new DateOnly(2026, 10, 1), rate: 100));
        await db.SaveChangesAsync(Ct);

        await db.Properties.Where(p => p.Id == roomType.PropertyId).ExecuteDeleteAsync(Ct);

        Assert.False(await db.RatePlans.AnyAsync(r => r.Id == parent.Id, Ct));
        Assert.False(await db.Restrictions.AnyAsync(r => r.RatePlanId == parent.Id, Ct));
        Assert.False(await db.Availability.AnyAsync(a => a.RoomTypeId == roomType.Id, Ct));
    }

    private async Task<(RoomType RoomType, RatePlan Parent)> SeedAsync()
    {
        var property = new Property("Seaside Resort", "EUR", "Europe/Istanbul", "TR", "Antalya");
        var roomType = new RoomType(property.Id, "DBL", "Double Room", countOfRooms: 10, maxAdults: 2, maxChildren: 1);
        var parent = new RatePlan(roomType.Id, "BAR", "Best Available", SellMode.PerPerson, MealPlan.Breakfast, 20);
        parent.SetOccupancies([new(1, -20), new(2, 0)], roomType.MaxAdults);

        await using var db = fixture.CreateDbContext();
        db.AddRange(property, roomType, parent);
        await db.SaveChangesAsync(Ct);

        return (roomType, parent);
    }
}
