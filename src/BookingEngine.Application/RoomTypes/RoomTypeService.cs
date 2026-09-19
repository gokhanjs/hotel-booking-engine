using BookingEngine.Application.Abstractions;
using BookingEngine.Application.Common;
using BookingEngine.Domain.RoomTypes;
using Microsoft.EntityFrameworkCore;

namespace BookingEngine.Application.RoomTypes;

public sealed class RoomTypeService(IBookingDbContext db)
{
    public async Task<Result<RoomTypeResponse>> CreateAsync(Guid propertyId, CreateRoomTypeRequest request, CancellationToken ct)
    {
        if (!await db.Properties.AnyAsync(p => p.Id == propertyId, ct))
        {
            return Error.NotFound("property");
        }

        var roomType = new RoomType(propertyId, request.Code, request.Name, request.CountOfRooms, request.MaxAdults, request.MaxChildren);
        if (await db.RoomTypes.AnyAsync(r => r.PropertyId == propertyId && r.Code == roomType.Code, ct))
        {
            return Error.Conflict("room_type.code_taken", $"Room type code '{roomType.Code}' is already used in this property.");
        }

        db.RoomTypes.Add(roomType);
        await db.SaveChangesAsync(ct);

        return RoomTypeResponse.From(roomType);
    }

    public async Task<Result<IReadOnlyList<RoomTypeResponse>>> ListAsync(Guid propertyId, CancellationToken ct)
    {
        if (!await db.Properties.AnyAsync(p => p.Id == propertyId, ct))
        {
            return Error.NotFound("property");
        }

        var roomTypes = await db.RoomTypes.AsNoTracking()
            .Where(r => r.PropertyId == propertyId)
            .OrderBy(r => r.Code)
            .ToListAsync(ct);

        return roomTypes.Select(RoomTypeResponse.From).ToList();
    }

    public async Task<Result<RoomTypeResponse>> GetAsync(Guid propertyId, Guid id, CancellationToken ct)
    {
        var roomType = await db.RoomTypes.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id && r.PropertyId == propertyId, ct);

        return roomType is null ? Error.NotFound("room_type") : RoomTypeResponse.From(roomType);
    }

    public async Task<Result<RoomTypeResponse>> UpdateAsync(Guid propertyId, Guid id, UpdateRoomTypeRequest request, CancellationToken ct)
    {
        var roomType = await db.RoomTypes.SingleOrDefaultAsync(r => r.Id == id && r.PropertyId == propertyId, ct);
        if (roomType is null)
        {
            return Error.NotFound("room_type");
        }

        var occupancies = await db.RatePlans.AsNoTracking()
            .Where(rp => rp.RoomTypeId == id)
            .Select(rp => rp.Occupancies)
            .ToListAsync(ct);

        if (occupancies.SelectMany(o => o).Any(o => o.Adults > request.MaxAdults))
        {
            return Error.Conflict(
                "room_type.max_adults_in_use",
                "Rate plans of this room type price more adults than the new maximum; update them first.");
        }

        roomType.Update(request.Name, request.CountOfRooms, request.MaxAdults, request.MaxChildren);
        await db.SaveChangesAsync(ct);

        return RoomTypeResponse.From(roomType);
    }

    public async Task<Result> DeleteAsync(Guid propertyId, Guid id, CancellationToken ct)
    {
        var hasExternalChildren = await db.RatePlans.AnyAsync(
            child => child.RoomTypeId != id && db.RatePlans.Any(parent => parent.RoomTypeId == id && parent.Id == child.ParentRatePlanId),
            ct);

        if (hasExternalChildren)
        {
            return Error.Conflict(
                "room_type.has_derived_dependents",
                "Rate plans of other room types derive from this room type's rate plans.");
        }

        var deleted = await db.RoomTypes.Where(r => r.Id == id && r.PropertyId == propertyId).ExecuteDeleteAsync(ct);

        return deleted == 0 ? Error.NotFound("room_type") : Result.Success();
    }
}
