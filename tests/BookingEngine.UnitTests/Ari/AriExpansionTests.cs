using BookingEngine.Application.Ari;

namespace BookingEngine.UnitTests.Ari;

public class AriExpansionTests
{
    private static readonly DateOnly _monday = new(2026, 10, 5);
    private static readonly Guid _roomTypeId = Guid.NewGuid();
    private static readonly Guid _ratePlanId = Guid.NewGuid();

    [Fact]
    public void Ranges_are_inclusive_on_both_ends()
    {
        var dates = AriExpansion.Dates(_monday, _monday.AddDays(2), days: null).ToList();

        Assert.Equal([_monday, _monday.AddDays(1), _monday.AddDays(2)], dates);
    }

    [Fact]
    public void Weekday_filter_keeps_only_matching_days()
    {
        var dates = AriExpansion.Dates(_monday, _monday.AddDays(13), [Weekday.Fr, Weekday.Sa]).ToList();

        Assert.Equal([_monday.AddDays(4), _monday.AddDays(5), _monday.AddDays(11), _monday.AddDays(12)], dates);
    }

    [Fact]
    public void Overlapping_availability_ranges_resolve_to_the_later_value()
    {
        var changes = AriExpansion.Expand(
        [
            new AvailabilityValue(_roomTypeId, _monday, _monday.AddDays(6), 5),
            new AvailabilityValue(_roomTypeId, _monday.AddDays(5), _monday.AddDays(6), 2),
        ]);

        Assert.Equal(7, changes.Count);
        Assert.Equal(5, changes.Single(c => c.Date == _monday.AddDays(4)).Availability);
        Assert.Equal(2, changes.Single(c => c.Date == _monday.AddDays(5)).Availability);
    }

    [Fact]
    public void Overlapping_restriction_values_merge_field_by_field()
    {
        var changes = AriExpansion.Expand(
        [
            new RestrictionValue(_ratePlanId, _monday, _monday, Rate: 100, MinStayArrival: 2),
            new RestrictionValue(_ratePlanId, _monday, _monday, Rate: 120, StopSell: true),
        ]);

        var change = Assert.Single(changes);
        Assert.Equal(120, change.Rate);
        Assert.Equal(2, change.MinStayArrival);
        Assert.True(change.StopSell);
        Assert.Null(change.ClosedToArrival);
    }
}
