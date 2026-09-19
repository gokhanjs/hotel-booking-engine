using BookingEngine.Domain.Common;
using BookingEngine.Domain.RatePlans;

namespace BookingEngine.UnitTests.Domain;

public class RatePlanTests
{
    private static readonly Guid _roomTypeId = Guid.NewGuid();

    private static RatePlan PerPerson(string code = "BAR") =>
        new(_roomTypeId, code, "Best Available", SellMode.PerPerson, MealPlan.Breakfast, childFee: 20);

    private static RatePlan PerRoom(string code = "FLEX") =>
        new(_roomTypeId, code, "Flexible", SellMode.PerRoom, MealPlan.RoomOnly, childFee: 0);

    [Fact]
    public void Code_is_normalized_to_upper_case()
    {
        Assert.Equal("BAR", PerPerson(" bar ").Code);
    }

    [Fact]
    public void Occupancies_are_stored_in_adult_order()
    {
        var plan = PerPerson();

        plan.SetOccupancies([new(2, 0), new(1, -20), new(3, 35)], maxAdults: 3);

        Assert.Equal([1, 2, 3], plan.Occupancies.Select(o => o.Adults));
    }

    [Fact]
    public void Occupancies_are_rejected_for_per_room_plans()
    {
        Assert.Throws<DomainException>(() => PerRoom().SetOccupancies([new(1, 0)], maxAdults: 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Occupancies_outside_room_capacity_are_rejected(int adults)
    {
        Assert.Throws<DomainException>(() => PerPerson().SetOccupancies([new(adults, 0)], maxAdults: 3));
    }

    [Fact]
    public void Duplicate_adult_counts_are_rejected()
    {
        Assert.Throws<DomainException>(() => PerPerson().SetOccupancies([new(2, 0), new(2, 10)], maxAdults: 3));
    }

    [Fact]
    public void Deriving_links_parent_and_drops_own_occupancies()
    {
        var parent = PerPerson("BAR");
        var child = PerPerson("NRF");
        child.SetOccupancies([new(1, -20), new(2, 0)], maxAdults: 2);

        child.DeriveFrom(parent, DerivedAdjustmentType.Percent, -10);

        Assert.True(child.IsDerived);
        Assert.Equal(parent.Id, child.Derived!.ParentRatePlanId);
        Assert.Empty(child.Occupancies);
    }

    [Fact]
    public void Derived_plans_cannot_define_occupancies()
    {
        var child = PerPerson("NRF");
        child.DeriveFrom(PerPerson("BAR"), DerivedAdjustmentType.Percent, -10);

        Assert.Throws<DomainException>(() => child.SetOccupancies([new(1, 0)], maxAdults: 2));
    }

    [Fact]
    public void Derivation_chains_are_rejected()
    {
        var root = PerRoom("FLEX");
        var middle = PerRoom("NRF");
        middle.DeriveFrom(root, DerivedAdjustmentType.Percent, -10);

        Assert.Throws<DomainException>(() => PerRoom("PROMO").DeriveFrom(middle, DerivedAdjustmentType.Amount, -5));
    }

    [Fact]
    public void Parent_must_share_sell_mode()
    {
        Assert.Throws<DomainException>(() => PerPerson().DeriveFrom(PerRoom(), DerivedAdjustmentType.Percent, -10));
    }

    [Fact]
    public void Plan_cannot_derive_from_itself()
    {
        var plan = PerRoom();

        Assert.Throws<DomainException>(() => plan.DeriveFrom(plan, DerivedAdjustmentType.Percent, -10));
    }
}
