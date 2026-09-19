namespace BookingEngine.Domain.Pricing;

public enum UnsellableReason
{
    ExceedsCapacity,
    NoAvailability,
    NoRate,
    OccupancyNotPriced,
    StopSell,
    ClosedToArrival,
    ClosedToDeparture,
    MinStayArrival,
    MinStayThrough,
    MaxStay,
}
