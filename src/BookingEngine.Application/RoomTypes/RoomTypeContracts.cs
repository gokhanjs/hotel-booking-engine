using System.ComponentModel.DataAnnotations;
using BookingEngine.Domain.RoomTypes;

namespace BookingEngine.Application.RoomTypes;

public sealed record CreateRoomTypeRequest(
    [property: Required, MaxLength(50)] string Code,
    [property: Required, MaxLength(200)] string Name,
    [property: Range(1, 10_000)] int CountOfRooms,
    [property: Range(1, 20)] int MaxAdults,
    [property: Range(0, 20)] int MaxChildren);

public sealed record UpdateRoomTypeRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: Range(1, 10_000)] int CountOfRooms,
    [property: Range(1, 20)] int MaxAdults,
    [property: Range(0, 20)] int MaxChildren);

public sealed record RoomTypeResponse(
    Guid Id,
    Guid PropertyId,
    string Code,
    string Name,
    int CountOfRooms,
    int MaxAdults,
    int MaxChildren,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static RoomTypeResponse From(RoomType r) =>
        new(r.Id, r.PropertyId, r.Code, r.Name, r.CountOfRooms, r.MaxAdults, r.MaxChildren, r.CreatedAt, r.UpdatedAt);
}
