using BookingEngine.Domain.Common;

namespace BookingEngine.Domain.Inventory;

public sealed class RoomTypeAvailability
{
    public Guid RoomTypeId { get; private set; }
    public DateOnly Date { get; private set; }
    public int Availability { get; private set; }

    private RoomTypeAvailability() { }

    public RoomTypeAvailability(Guid roomTypeId, DateOnly date, int availability)
    {
        RoomTypeId = roomTypeId;
        Date = date;
        Availability = Guard.AtLeast(availability, 0, "Availability");
    }
}
