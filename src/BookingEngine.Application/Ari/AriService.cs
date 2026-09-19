using BookingEngine.Application.Abstractions;
using BookingEngine.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace BookingEngine.Application.Ari;

public sealed class AriService(IBookingDbContext db, IAriWriter writer, TimeProvider clock)
{
    public const int MaxDaysAhead = 730;
    public const int MaxDatesPerRequest = 20_000;
    public const int MaxReadRangeDays = 366;

    public async Task<Result<AriUpdateResponse>> UpdateAvailabilityAsync(Guid propertyId, AvailabilityUpdateRequest request, CancellationToken ct)
    {
        var window = await WindowAsync(propertyId, ct);
        if (!window.IsSuccess)
        {
            return window.Error;
        }

        var rangeError = CheckRanges(request.Values.Select(v => (v.DateFrom, v.DateTo)), window.Value!);
        if (rangeError is not null)
        {
            return rangeError;
        }

        var roomIds = request.Values.Select(v => v.RoomTypeId).Distinct().ToList();
        var capacity = await db.RoomTypes
            .Where(r => r.PropertyId == propertyId && roomIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.CountOfRooms, ct);

        if (capacity.Count != roomIds.Count)
        {
            return Error.Unprocessable("ari.unknown_room_type", "One or more room types do not belong to this property.");
        }

        if (request.Values.FirstOrDefault(v => v.Availability > capacity[v.RoomTypeId]) is { } over)
        {
            return Error.Unprocessable(
                "ari.availability_exceeds_rooms",
                $"Availability {over.Availability} exceeds the {capacity[over.RoomTypeId]} physical rooms of room type {over.RoomTypeId}.");
        }

        var changes = AriExpansion.Expand(request.Values);
        if (changes.Count > MaxDatesPerRequest)
        {
            return TooManyDates();
        }

        await writer.UpsertAvailabilityAsync(propertyId, changes, ct);

        return new AriUpdateResponse(changes.Count);
    }

    public async Task<Result<AriUpdateResponse>> UpdateRestrictionsAsync(Guid propertyId, RestrictionUpdateRequest request, CancellationToken ct)
    {
        var window = await WindowAsync(propertyId, ct);
        if (!window.IsSuccess)
        {
            return window.Error;
        }

        var rangeError = CheckRanges(request.Values.Select(v => (v.DateFrom, v.DateTo)), window.Value!);
        if (rangeError is not null)
        {
            return rangeError;
        }

        if (request.Values.Any(IsEmpty))
        {
            return Error.Unprocessable("ari.empty_update", "Each value must set at least one rate or restriction field.");
        }

        var planIds = request.Values.Select(v => v.RatePlanId).Distinct().ToList();
        var plans = await db.RatePlans
            .Where(rp => planIds.Contains(rp.Id) && db.RoomTypes.Any(rt => rt.Id == rp.RoomTypeId && rt.PropertyId == propertyId))
            .Select(rp => new { rp.Id, rp.ParentRatePlanId })
            .ToListAsync(ct);

        if (plans.Count != planIds.Count)
        {
            return Error.Unprocessable("ari.unknown_rate_plan", "One or more rate plans do not belong to this property.");
        }

        var derived = plans.Where(p => p.ParentRatePlanId is not null).Select(p => p.Id).ToHashSet();
        if (request.Values.Any(v => v.Rate is not null && derived.Contains(v.RatePlanId)))
        {
            return Error.Unprocessable("ari.rate_on_derived_plan", "Derived rate plans take their rate from the parent plan.");
        }

        var changes = AriExpansion.Expand(request.Values);
        if (changes.Count > MaxDatesPerRequest)
        {
            return TooManyDates();
        }

        await writer.UpsertRestrictionsAsync(propertyId, changes, ct);

        return new AriUpdateResponse(changes.Count);
    }

    public async Task<Result<IReadOnlyList<AvailabilityResponse>>> GetAvailabilityAsync(
        Guid propertyId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (await CheckReadAsync(propertyId, from, to, ct) is { } error)
        {
            return error;
        }

        return await db.Availability.AsNoTracking()
            .Where(a => a.Date >= from && a.Date <= to && db.RoomTypes.Any(rt => rt.Id == a.RoomTypeId && rt.PropertyId == propertyId))
            .OrderBy(a => a.RoomTypeId).ThenBy(a => a.Date)
            .Select(a => new AvailabilityResponse(a.RoomTypeId, a.Date, a.Availability))
            .ToListAsync(ct);
    }

    public async Task<Result<IReadOnlyList<RestrictionResponse>>> GetRestrictionsAsync(
        Guid propertyId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (await CheckReadAsync(propertyId, from, to, ct) is { } error)
        {
            return error;
        }

        return await db.Restrictions.AsNoTracking()
            .Where(r => r.Date >= from && r.Date <= to && db.RatePlans.Any(rp =>
                rp.Id == r.RatePlanId && db.RoomTypes.Any(rt => rt.Id == rp.RoomTypeId && rt.PropertyId == propertyId)))
            .OrderBy(r => r.RatePlanId).ThenBy(r => r.Date)
            .Select(r => new RestrictionResponse(
                r.RatePlanId, r.Date, r.Rate, r.StopSell, r.ClosedToArrival, r.ClosedToDeparture, r.MinStayArrival, r.MinStayThrough, r.MaxStay))
            .ToListAsync(ct);
    }

    private sealed record DateWindow(DateOnly Earliest, DateOnly Latest);

    private async Task<Result<DateWindow>> WindowAsync(Guid propertyId, CancellationToken ct)
    {
        var property = await db.Properties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == propertyId, ct);
        if (property is null)
        {
            return Error.NotFound("property");
        }

        var today = property.Today(clock.GetUtcNow());
        return new DateWindow(today, today.AddDays(MaxDaysAhead));
    }

    private static Error? CheckRanges(IEnumerable<(DateOnly From, DateOnly To)> ranges, DateWindow window)
    {
        foreach (var (from, to) in ranges)
        {
            if (to < from)
            {
                return Error.Unprocessable("ari.invalid_range", $"date_to {to:yyyy-MM-dd} is before date_from {from:yyyy-MM-dd}.");
            }

            if (from < window.Earliest || to > window.Latest)
            {
                return Error.Unprocessable(
                    "ari.outside_window",
                    $"Dates must fall between {window.Earliest:yyyy-MM-dd} and {window.Latest:yyyy-MM-dd} (property local time).");
            }
        }

        return null;
    }

    private async Task<Error?> CheckReadAsync(Guid propertyId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from || to.DayNumber - from.DayNumber >= MaxReadRangeDays)
        {
            return Error.Unprocessable("ari.invalid_range", $"Use a date range of 1 to {MaxReadRangeDays} days.");
        }

        return await db.Properties.AnyAsync(p => p.Id == propertyId, ct) ? null : Error.NotFound("property");
    }

    private static bool IsEmpty(RestrictionValue v) =>
        v is { Rate: null, StopSell: null, ClosedToArrival: null, ClosedToDeparture: null, MinStayArrival: null, MinStayThrough: null, MaxStay: null };

    private static Error TooManyDates() =>
        Error.Unprocessable("ari.too_many_dates", $"A single request may change at most {MaxDatesPerRequest} dates.");
}
