using BookingEngine.Application.Abstractions;
using BookingEngine.Application.Ari;
using BookingEngine.Application.Common;
using BookingEngine.Domain.Pricing;
using BookingEngine.Domain.Properties;
using Microsoft.EntityFrameworkCore;

namespace BookingEngine.Application.Storefront;

public sealed class StorefrontService(IBookingDbContext db, IPropertyCache cache, TimeProvider clock)
{
    public const int MaxNights = 30;
    public const int MaxCalendarDays = 92;

    public async Task<Result<StorefrontPropertyResponse>> GetPropertyAsync(Guid propertyId, CancellationToken ct)
    {
        var property = await db.Properties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == propertyId, ct);
        if (property is null)
        {
            return Error.NotFound("property");
        }

        return await cache.GetOrCreateAsync(propertyId, "detail", async token =>
        {
            var roomTypes = await db.RoomTypes.AsNoTracking().Where(r => r.PropertyId == propertyId).OrderBy(r => r.Code).ToListAsync(token);
            var roomIds = roomTypes.Select(r => r.Id).ToList();
            var plans = await db.RatePlans.AsNoTracking().Where(p => roomIds.Contains(p.RoomTypeId)).OrderBy(p => p.Code).ToListAsync(token);

            return new StorefrontPropertyResponse(
                property.Id,
                property.Name,
                property.Currency,
                property.Timezone,
                property.CountryCode,
                property.City,
                property.Address,
                property.Latitude,
                property.Longitude,
                roomTypes.Select(r => new StorefrontRoomType(
                    r.Id,
                    r.Code,
                    r.Name,
                    r.MaxAdults,
                    r.MaxChildren,
                    plans.Where(p => p.RoomTypeId == r.Id).Select(p => new StorefrontRatePlan(p.Id, p.Code, p.Name, p.MealPlan)).ToList())).ToList());
        }, ct);
    }

    public async Task<Result<SearchResponse>> SearchAsync(
        Guid propertyId, DateOnly checkin, DateOnly checkout, int adults, int children, CancellationToken ct)
    {
        var property = await db.Properties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == propertyId, ct);
        if (property is null)
        {
            return Error.NotFound("property");
        }

        if (checkout <= checkin)
        {
            return Error.Unprocessable("search.invalid_dates", "checkout must be after checkin.");
        }

        if (checkout.DayNumber - checkin.DayNumber > MaxNights)
        {
            return Error.Unprocessable("search.stay_too_long", $"Stays are limited to {MaxNights} nights.");
        }

        if (OutsideWindow(property, checkin, checkout) is { } windowError)
        {
            return windowError;
        }

        var stay = new Stay(checkin, checkout, adults, children);
        var key = $"search:{checkin:yyyyMMdd}:{checkout:yyyyMMdd}:{adults}:{children}";

        return await cache.GetOrCreateAsync(propertyId, key, async token =>
        {
            var inventories = await LoadInventoryAsync(propertyId, checkin, checkout, token);
            var offers = inventories
                .Select(inventory => (inventory, outcome: StayPricer.Price(stay, inventory)))
                .Where(x => x.outcome.IsSellable)
                .Select(x => new Offer(
                    x.inventory.RoomType.Id,
                    x.inventory.RoomType.Code,
                    x.inventory.RoomType.Name,
                    x.inventory.Plan.Id,
                    x.inventory.Plan.Code,
                    x.inventory.Plan.Name,
                    x.inventory.Plan.MealPlan,
                    x.outcome.Total,
                    x.outcome.Nights.Select(n => new NightlyPrice(n.Date, n.Price)).ToList()))
                .OrderBy(o => o.TotalPrice).ThenBy(o => o.RoomTypeCode).ThenBy(o => o.RatePlanCode)
                .ToList();

            return new SearchResponse(property.Id, property.Currency, checkin, checkout, stay.Nights, adults, children, offers);
        }, ct);
    }

    public async Task<Result<CalendarResponse>> CalendarAsync(
        Guid propertyId, DateOnly from, DateOnly to, int adults, int children, CancellationToken ct)
    {
        var property = await db.Properties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == propertyId, ct);
        if (property is null)
        {
            return Error.NotFound("property");
        }

        if (to < from || to.DayNumber - from.DayNumber >= MaxCalendarDays)
        {
            return Error.Unprocessable("calendar.invalid_range", $"Use a range of 1 to {MaxCalendarDays} days.");
        }

        if (OutsideWindow(property, from, to) is { } windowError)
        {
            return windowError;
        }

        var key = $"calendar:{from:yyyyMMdd}:{to:yyyyMMdd}:{adults}:{children}";

        return await cache.GetOrCreateAsync(propertyId, key, async token =>
        {
            var inventories = await LoadInventoryAsync(propertyId, from, to, token);
            var days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
                .Select(from.AddDays)
                .Select(date =>
                {
                    var min = inventories
                        .Select(inventory => StayPricer.SellableNightPrice(inventory, date, adults, children))
                        .Where(price => price is not null)
                        .Min();
                    return new CalendarDay(date, min is not null, min);
                })
                .ToList();

            return new CalendarResponse(property.Id, property.Currency, adults, children, days);
        }, ct);
    }

    private Error? OutsideWindow(Property property, DateOnly from, DateOnly to)
    {
        var today = property.Today(clock.GetUtcNow());
        var latest = today.AddDays(AriService.MaxDaysAhead);

        return from < today || to > latest
            ? Error.Unprocessable("search.outside_window", $"Dates must fall between {today:yyyy-MM-dd} and {latest:yyyy-MM-dd} (property local time).")
            : null;
    }

    private async Task<List<RateInventory>> LoadInventoryAsync(Guid propertyId, DateOnly from, DateOnly toInclusive, CancellationToken ct)
    {
        var roomTypes = await db.RoomTypes.AsNoTracking().Where(r => r.PropertyId == propertyId).ToListAsync(ct);
        var roomIds = roomTypes.Select(r => r.Id).ToList();
        var plans = await db.RatePlans.AsNoTracking().Where(p => roomIds.Contains(p.RoomTypeId)).ToListAsync(ct);
        var planIds = plans.Select(p => p.Id).ToList();

        var availability = (await db.Availability.AsNoTracking()
                .Where(a => roomIds.Contains(a.RoomTypeId) && a.Date >= from && a.Date <= toInclusive)
                .ToListAsync(ct))
            .ToLookup(a => a.RoomTypeId);
        var restrictions = (await db.Restrictions.AsNoTracking()
                .Where(r => planIds.Contains(r.RatePlanId) && r.Date >= from && r.Date <= toInclusive)
                .ToListAsync(ct))
            .ToLookup(r => r.RatePlanId);

        var plansById = plans.ToDictionary(p => p.Id);
        var roomTypesById = roomTypes.ToDictionary(r => r.Id);

        return plans.Select(plan =>
        {
            var parent = plan.ParentRatePlanId is { } parentId ? plansById.GetValueOrDefault(parentId) : null;
            return new RateInventory(
                roomTypesById[plan.RoomTypeId],
                plan,
                parent,
                availability[plan.RoomTypeId].ToDictionary(a => a.Date, a => a.Availability),
                restrictions[plan.Id].ToDictionary(r => r.Date),
                parent is null ? new Dictionary<DateOnly, Domain.Inventory.RatePlanRestriction>() : restrictions[parent.Id].ToDictionary(r => r.Date));
        }).ToList();
    }
}
