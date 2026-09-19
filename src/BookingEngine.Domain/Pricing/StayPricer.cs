using BookingEngine.Domain.RatePlans;

namespace BookingEngine.Domain.Pricing;

public sealed record NightlyRate(DateOnly Date, decimal Price);

public sealed record PricingOutcome(IReadOnlyList<NightlyRate> Nights, IReadOnlySet<UnsellableReason> Reasons)
{
    public bool IsSellable => Reasons.Count == 0;

    public decimal Total => Nights.Sum(n => n.Price);
}

public static class StayPricer
{
    public static PricingOutcome Price(Stay stay, RateInventory inventory)
    {
        var reasons = new HashSet<UnsellableReason>();
        if (stay.Adults > inventory.RoomType.MaxAdults || stay.Children > inventory.RoomType.MaxChildren)
        {
            reasons.Add(UnsellableReason.ExceedsCapacity);
        }

        var nights = new List<NightlyRate>(stay.Nights);
        var minStayThrough = 1;
        foreach (var date in stay.NightDates)
        {
            if (inventory.Availability.GetValueOrDefault(date) < 1)
            {
                reasons.Add(UnsellableReason.NoAvailability);
            }

            if (!inventory.Restrictions.TryGetValue(date, out var restriction))
            {
                reasons.Add(UnsellableReason.NoRate);
                continue;
            }

            if (restriction.StopSell)
            {
                reasons.Add(UnsellableReason.StopSell);
            }

            minStayThrough = Math.Max(minStayThrough, restriction.MinStayThrough);

            var (price, reason) = NightPrice(inventory, date, stay.Adults, stay.Children);
            if (reason is { } r)
            {
                reasons.Add(r);
            }
            else
            {
                nights.Add(new NightlyRate(date, price));
            }
        }

        if (inventory.Restrictions.TryGetValue(stay.Checkin, out var arrival))
        {
            if (arrival.ClosedToArrival)
            {
                reasons.Add(UnsellableReason.ClosedToArrival);
            }

            if (stay.Nights < arrival.MinStayArrival)
            {
                reasons.Add(UnsellableReason.MinStayArrival);
            }

            if (arrival.MaxStay is { } maxStay && stay.Nights > maxStay)
            {
                reasons.Add(UnsellableReason.MaxStay);
            }
        }

        if (stay.Nights < minStayThrough)
        {
            reasons.Add(UnsellableReason.MinStayThrough);
        }

        if (inventory.Restrictions.TryGetValue(stay.Checkout, out var departure) && departure.ClosedToDeparture)
        {
            reasons.Add(UnsellableReason.ClosedToDeparture);
        }

        return new PricingOutcome(reasons.Count == 0 ? nights : [], reasons);
    }

    /// <summary>Price of a single sellable night, ignoring length-of-stay and arrival/departure rules.</summary>
    public static decimal? SellableNightPrice(RateInventory inventory, DateOnly date, int adults, int children)
    {
        if (adults > inventory.RoomType.MaxAdults || children > inventory.RoomType.MaxChildren
            || inventory.Availability.GetValueOrDefault(date) < 1
            || !inventory.Restrictions.TryGetValue(date, out var restriction) || restriction.StopSell)
        {
            return null;
        }

        var (price, reason) = NightPrice(inventory, date, adults, children);
        return reason is null ? price : null;
    }

    // Derived plans adjust the parent's occupancy price; the child fee always comes from the plan being sold.
    private static (decimal Price, UnsellableReason? Reason) NightPrice(RateInventory inventory, DateOnly date, int adults, int children)
    {
        var plan = inventory.Plan;
        var source = plan.IsDerived ? inventory.Parent : plan;
        var restrictions = plan.IsDerived ? inventory.ParentRestrictions : inventory.Restrictions;

        if (source is null || restrictions.GetValueOrDefault(date)?.Rate is not { } baseRate)
        {
            return (0, UnsellableReason.NoRate);
        }

        var price = baseRate;
        if (plan.SellMode == SellMode.PerPerson)
        {
            var occupancy = source.Occupancies.FirstOrDefault(o => o.Adults == adults);
            if (occupancy is null)
            {
                return (0, UnsellableReason.OccupancyNotPriced);
            }

            price = Math.Max(0, price + occupancy.PriceAdjustment);
        }

        if (plan.Derived is { } derived)
        {
            price = derived.Apply(price);
        }

        price += children * plan.ChildFee;

        return (Math.Round(price, 2, MidpointRounding.AwayFromZero), null);
    }
}
