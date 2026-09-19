using BookingEngine.Domain.Inventory;
using BookingEngine.Domain.RatePlans;
using BookingEngine.Domain.RoomTypes;

namespace BookingEngine.Domain.Pricing;

/// <summary>ARI snapshot for one sellable room type and rate plan pair; restrictions should include the departure date.</summary>
public sealed record RateInventory(
    RoomType RoomType,
    RatePlan Plan,
    RatePlan? Parent,
    IReadOnlyDictionary<DateOnly, int> Availability,
    IReadOnlyDictionary<DateOnly, RatePlanRestriction> Restrictions,
    IReadOnlyDictionary<DateOnly, RatePlanRestriction> ParentRestrictions);
