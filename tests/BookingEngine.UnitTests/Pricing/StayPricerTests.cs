using BookingEngine.Domain.Inventory;
using BookingEngine.Domain.Pricing;
using BookingEngine.Domain.RatePlans;
using BookingEngine.Domain.RoomTypes;

namespace BookingEngine.UnitTests.Pricing;

public class StayPricerTests
{
    private static readonly DateOnly _day0 = new(2026, 10, 5);
    private readonly RoomType _room = new(Guid.NewGuid(), "DBL", "Double", countOfRooms: 10, maxAdults: 3, maxChildren: 2);
    private readonly Dictionary<DateOnly, int> _availability = [];
    private readonly Dictionary<DateOnly, RatePlanRestriction> _restrictions = [];
    private readonly Dictionary<DateOnly, RatePlanRestriction> _parentRestrictions = [];

    private RatePlan PerRoomPlan(decimal childFee = 0) =>
        new(_room.Id, "FLEX", "Flexible", SellMode.PerRoom, MealPlan.RoomOnly, childFee);

    private RatePlan PerPersonPlan()
    {
        var plan = new RatePlan(_room.Id, "BAR", "Best", SellMode.PerPerson, MealPlan.Breakfast, childFee: 15);
        plan.SetOccupancies([new(1, -20), new(2, 0)], _room.MaxAdults);
        return plan;
    }

    private void Open(Guid planId, int nights, decimal rate = 100, int availability = 5)
    {
        for (var i = 0; i < nights; i++)
        {
            _availability[_day0.AddDays(i)] = availability;
            _restrictions[_day0.AddDays(i)] = new RatePlanRestriction(planId, _day0.AddDays(i), rate);
        }
    }

    private void Restrict(DateOnly date, Guid planId, decimal? rate = 100, bool stopSell = false, bool cta = false, bool ctd = false,
        int minStayArrival = 1, int minStayThrough = 1, int? maxStay = null) =>
        _restrictions[date] = new RatePlanRestriction(planId, date, rate, stopSell, cta, ctd, minStayArrival, minStayThrough, maxStay);

    private PricingOutcome Price(RatePlan plan, int nights, int adults = 2, int children = 0, RatePlan? parent = null) =>
        StayPricer.Price(
            new Stay(_day0, _day0.AddDays(nights), adults, children),
            new RateInventory(_room, plan, parent, _availability, _restrictions, _parentRestrictions));

    [Fact]
    public void Per_room_stay_sums_nightly_rates_plus_child_fees()
    {
        var plan = PerRoomPlan(childFee: 12.5m);
        Open(plan.Id, nights: 3);
        Restrict(_day0.AddDays(1), plan.Id, rate: 150);

        var outcome = Price(plan, nights: 3, children: 2);

        Assert.True(outcome.IsSellable);
        Assert.Equal([125m, 175m, 125m], outcome.Nights.Select(n => n.Price));
        Assert.Equal(425m, outcome.Total);
    }

    [Theory]
    [InlineData(1, 80)]
    [InlineData(2, 100)]
    public void Per_person_stay_applies_occupancy_adjustment(int adults, decimal nightly)
    {
        var plan = PerPersonPlan();
        Open(plan.Id, nights: 2);

        Assert.Equal(nightly * 2, Price(plan, nights: 2, adults: adults).Total);
    }

    [Fact]
    public void Unpriced_occupancy_is_not_sellable()
    {
        var plan = PerPersonPlan();
        Open(plan.Id, nights: 1);

        Assert.Contains(UnsellableReason.OccupancyNotPriced, Price(plan, nights: 1, adults: 3).Reasons);
    }

    [Fact]
    public void Derived_plan_adjusts_the_parent_occupancy_price_and_adds_its_own_child_fee()
    {
        var parent = PerPersonPlan();
        var child = new RatePlan(_room.Id, "NRF", "Non-refundable", SellMode.PerPerson, MealPlan.Breakfast, childFee: 10);
        child.DeriveFrom(parent, DerivedAdjustmentType.Percent, -10);
        Open(child.Id, nights: 1, rate: 0);
        _restrictions[_day0] = new RatePlanRestriction(child.Id, _day0, rate: null);
        _parentRestrictions[_day0] = new RatePlanRestriction(parent.Id, _day0, rate: 100);

        var outcome = Price(child, nights: 1, adults: 1, children: 1, parent: parent);

        Assert.Equal(82m, outcome.Total);
    }

    [Fact]
    public void Derived_plan_without_parent_rate_is_not_sellable()
    {
        var parent = PerRoomPlan();
        var child = new RatePlan(_room.Id, "NRF", "Non-refundable", SellMode.PerRoom, MealPlan.RoomOnly, 0);
        child.DeriveFrom(parent, DerivedAdjustmentType.Amount, -10);
        Open(child.Id, nights: 1);

        Assert.Contains(UnsellableReason.NoRate, Price(child, nights: 1, parent: parent).Reasons);
    }

    [Fact]
    public void Missing_availability_or_restriction_rows_close_the_night()
    {
        var plan = PerRoomPlan();
        Open(plan.Id, nights: 3);
        _availability.Remove(_day0.AddDays(1));
        _restrictions.Remove(_day0.AddDays(2));

        Assert.Equal([UnsellableReason.NoAvailability, UnsellableReason.NoRate], Price(plan, nights: 3).Reasons.Order());
    }

    [Fact]
    public void Zero_availability_is_not_sellable()
    {
        var plan = PerRoomPlan();
        Open(plan.Id, nights: 2, availability: 0);

        Assert.Contains(UnsellableReason.NoAvailability, Price(plan, nights: 2).Reasons);
    }

    [Fact]
    public void Stop_sell_on_any_night_blocks_the_stay()
    {
        var plan = PerRoomPlan();
        Open(plan.Id, nights: 3);
        Restrict(_day0.AddDays(2), plan.Id, stopSell: true);

        Assert.Equal([UnsellableReason.StopSell], Price(plan, nights: 3).Reasons);
    }

    [Fact]
    public void Closed_to_arrival_only_matters_on_the_arrival_date()
    {
        var plan = PerRoomPlan();
        Open(plan.Id, nights: 3);
        Restrict(_day0.AddDays(1), plan.Id, cta: true);

        Assert.True(Price(plan, nights: 3).IsSellable);

        Restrict(_day0, plan.Id, cta: true);
        Assert.Equal([UnsellableReason.ClosedToArrival], Price(plan, nights: 3).Reasons);
    }

    [Fact]
    public void Closed_to_departure_is_checked_on_the_checkout_date_which_is_not_a_night()
    {
        var plan = PerRoomPlan();
        Open(plan.Id, nights: 2);
        Restrict(_day0.AddDays(2), plan.Id, rate: null, ctd: true);

        Assert.Equal([UnsellableReason.ClosedToDeparture], Price(plan, nights: 2).Reasons);
        Assert.True(Price(plan, nights: 1).IsSellable);
    }

    [Fact]
    public void Min_stay_arrival_uses_only_the_arrival_date_value()
    {
        var plan = PerRoomPlan();
        Open(plan.Id, nights: 3);
        Restrict(_day0.AddDays(1), plan.Id, minStayArrival: 5);

        Assert.True(Price(plan, nights: 2).IsSellable);

        Restrict(_day0, plan.Id, minStayArrival: 3);
        Assert.Equal([UnsellableReason.MinStayArrival], Price(plan, nights: 2).Reasons);
        Assert.True(Price(plan, nights: 3).IsSellable);
    }

    [Fact]
    public void Min_stay_through_uses_the_highest_value_across_the_stay()
    {
        var plan = PerRoomPlan();
        Open(plan.Id, nights: 3);
        Restrict(_day0.AddDays(1), plan.Id, minStayThrough: 3);

        Assert.Equal([UnsellableReason.MinStayThrough], Price(plan, nights: 2).Reasons);
        Assert.True(Price(plan, nights: 3).IsSellable);
    }

    [Fact]
    public void Max_stay_is_evaluated_on_the_arrival_date()
    {
        var plan = PerRoomPlan();
        Open(plan.Id, nights: 4);
        Restrict(_day0, plan.Id, maxStay: 3);

        Assert.Equal([UnsellableReason.MaxStay], Price(plan, nights: 4).Reasons);
        Assert.True(Price(plan, nights: 3).IsSellable);
    }

    [Fact]
    public void Guests_beyond_room_capacity_are_rejected()
    {
        var plan = PerRoomPlan();
        Open(plan.Id, nights: 1);

        Assert.Contains(UnsellableReason.ExceedsCapacity, Price(plan, nights: 1, adults: 4).Reasons);
        Assert.Contains(UnsellableReason.ExceedsCapacity, Price(plan, nights: 1, children: 3).Reasons);
    }

    [Fact]
    public void Single_night_price_ignores_stay_rules_but_respects_stop_sell()
    {
        var plan = PerRoomPlan();
        Open(plan.Id, nights: 2);
        Restrict(_day0, plan.Id, cta: true, minStayArrival: 7);
        Restrict(_day0.AddDays(1), plan.Id, stopSell: true);
        var inventory = new RateInventory(_room, plan, null, _availability, _restrictions, _parentRestrictions);

        Assert.Equal(100m, StayPricer.SellableNightPrice(inventory, _day0, adults: 2, children: 0));
        Assert.Null(StayPricer.SellableNightPrice(inventory, _day0.AddDays(1), adults: 2, children: 0));
    }
}
