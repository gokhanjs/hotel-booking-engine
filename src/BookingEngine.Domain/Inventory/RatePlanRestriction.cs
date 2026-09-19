using BookingEngine.Domain.Common;

namespace BookingEngine.Domain.Inventory;

public sealed class RatePlanRestriction
{
    public Guid RatePlanId { get; private set; }
    public DateOnly Date { get; private set; }

    /// <summary>Base nightly rate; null for derived rate plans, whose rate comes from the parent.</summary>
    public decimal? Rate { get; private set; }
    public bool StopSell { get; private set; }
    public bool ClosedToArrival { get; private set; }
    public bool ClosedToDeparture { get; private set; }
    public int MinStayArrival { get; private set; } = 1;
    public int MinStayThrough { get; private set; } = 1;

    /// <summary>Maximum nights for stays arriving on this date; null means unlimited.</summary>
    public int? MaxStay { get; private set; }

    private RatePlanRestriction() { }

    public RatePlanRestriction(
        Guid ratePlanId,
        DateOnly date,
        decimal? rate,
        bool stopSell = false,
        bool closedToArrival = false,
        bool closedToDeparture = false,
        int minStayArrival = 1,
        int minStayThrough = 1,
        int? maxStay = null)
    {
        RatePlanId = ratePlanId;
        Date = date;
        Rate = rate is { } r ? Guard.NotNegative(r, "Rate") : null;
        StopSell = stopSell;
        ClosedToArrival = closedToArrival;
        ClosedToDeparture = closedToDeparture;
        MinStayArrival = Guard.AtLeast(minStayArrival, 1, "Min stay arrival");
        MinStayThrough = Guard.AtLeast(minStayThrough, 1, "Min stay through");
        MaxStay = maxStay is { } m ? Guard.AtLeast(m, 1, "Max stay") : null;
    }
}
