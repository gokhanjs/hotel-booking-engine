using BookingEngine.Domain.Common;

namespace BookingEngine.Domain.Pricing;

public sealed record Stay
{
    public Stay(DateOnly checkin, DateOnly checkout, int adults, int children)
    {
        if (checkout <= checkin)
        {
            throw new DomainException("Checkout must be after checkin.");
        }

        Checkin = checkin;
        Checkout = checkout;
        Adults = Guard.AtLeast(adults, 1, "Adults");
        Children = Guard.AtLeast(children, 0, "Children");
    }

    public DateOnly Checkin { get; }
    public DateOnly Checkout { get; }
    public int Adults { get; }
    public int Children { get; }

    public int Nights => Checkout.DayNumber - Checkin.DayNumber;

    public IEnumerable<DateOnly> NightDates => Enumerable.Range(0, Nights).Select(Checkin.AddDays);
}
