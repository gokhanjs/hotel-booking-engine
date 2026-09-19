using BookingEngine.Application.Abstractions;
using BookingEngine.Application.Common;
using BookingEngine.Domain.Properties;
using Microsoft.EntityFrameworkCore;

namespace BookingEngine.Application.Properties;

public sealed class PropertyService(IBookingDbContext db)
{
    public async Task<PropertyResponse> CreateAsync(CreatePropertyRequest request, CancellationToken ct)
    {
        var property = new Property(request.Name, request.Currency, request.Timezone, request.CountryCode, request.City);
        property.SetLocation(request.Address, request.Latitude, request.Longitude);

        db.Properties.Add(property);
        await db.SaveChangesAsync(ct);

        return PropertyResponse.From(property);
    }

    public async Task<PagedResponse<PropertyResponse>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var total = await db.Properties.CountAsync(ct);
        var items = await db.Properties
            .AsNoTracking()
            .OrderBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<PropertyResponse>(
            items.Select(PropertyResponse.From).ToList(),
            new PageMeta(page, pageSize, total));
    }

    public async Task<Result<PropertyResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        var property = await db.Properties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);

        return property is null ? Error.NotFound("property") : PropertyResponse.From(property);
    }

    public async Task<Result<PropertyResponse>> UpdateAsync(Guid id, UpdatePropertyRequest request, CancellationToken ct)
    {
        var property = await db.Properties.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (property is null)
        {
            return Error.NotFound("property");
        }

        property.UpdateDetails(request.Name, request.Timezone, request.CountryCode, request.City);
        property.SetLocation(request.Address, request.Latitude, request.Longitude);
        await db.SaveChangesAsync(ct);

        return PropertyResponse.From(property);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct)
    {
        var deleted = await db.Properties.Where(p => p.Id == id).ExecuteDeleteAsync(ct);

        return deleted == 0 ? Error.NotFound("property") : Result.Success();
    }
}
