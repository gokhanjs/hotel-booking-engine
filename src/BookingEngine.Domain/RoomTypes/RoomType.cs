using BookingEngine.Domain.Common;

namespace BookingEngine.Domain.RoomTypes;

public sealed class RoomType : Entity
{
    public Guid PropertyId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int CountOfRooms { get; private set; }
    public int MaxAdults { get; private set; }
    public int MaxChildren { get; private set; }

    private RoomType() { }

    public RoomType(Guid propertyId, string code, string name, int countOfRooms, int maxAdults, int maxChildren)
    {
        PropertyId = propertyId;
        Code = Guard.NotBlank(code, "Code", 50).ToUpperInvariant();
        Update(name, countOfRooms, maxAdults, maxChildren);
    }

    public void Update(string name, int countOfRooms, int maxAdults, int maxChildren)
    {
        Name = Guard.NotBlank(name, "Name", 200);
        CountOfRooms = Guard.AtLeast(countOfRooms, 1, "Count of rooms");
        MaxAdults = Guard.AtLeast(maxAdults, 1, "Max adults");
        MaxChildren = Guard.AtLeast(maxChildren, 0, "Max children");
    }
}
