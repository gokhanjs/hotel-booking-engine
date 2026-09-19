namespace BookingEngine.Application.Ari;

public sealed record AvailabilityChange(Guid RoomTypeId, DateOnly Date, int Availability);

/// <summary>A per-date restriction patch; null fields keep the stored value, and MaxStay 0 removes the limit.</summary>
public sealed record RestrictionChange(
    Guid RatePlanId,
    DateOnly Date,
    decimal? Rate,
    bool? StopSell,
    bool? ClosedToArrival,
    bool? ClosedToDeparture,
    int? MinStayArrival,
    int? MinStayThrough,
    int? MaxStay);

public static class AriExpansion
{
    public static IEnumerable<DateOnly> Dates(DateOnly from, DateOnly to, IReadOnlyList<Weekday>? days)
    {
        var allowed = days is { Count: > 0 } ? days.Select(ToDayOfWeek).ToHashSet() : null;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (allowed is null || allowed.Contains(date.DayOfWeek))
            {
                yield return date;
            }
        }
    }

    public static IReadOnlyList<AvailabilityChange> Expand(IEnumerable<AvailabilityValue> values)
    {
        var changes = new Dictionary<(Guid, DateOnly), AvailabilityChange>();
        foreach (var value in values)
        {
            foreach (var date in Dates(value.DateFrom, value.DateTo, value.Days))
            {
                changes[(value.RoomTypeId, date)] = new AvailabilityChange(value.RoomTypeId, date, value.Availability);
            }
        }

        return [.. changes.Values];
    }

    public static IReadOnlyList<RestrictionChange> Expand(IEnumerable<RestrictionValue> values)
    {
        var changes = new Dictionary<(Guid, DateOnly), RestrictionChange>();
        foreach (var value in values)
        {
            foreach (var date in Dates(value.DateFrom, value.DateTo, value.Days))
            {
                var key = (value.RatePlanId, date);
                changes[key] = changes.TryGetValue(key, out var earlier)
                    ? Merge(earlier, value)
                    : new RestrictionChange(
                        value.RatePlanId, date, value.Rate, value.StopSell, value.ClosedToArrival, value.ClosedToDeparture,
                        value.MinStayArrival, value.MinStayThrough, value.MaxStay);
            }
        }

        return [.. changes.Values];
    }

    private static RestrictionChange Merge(RestrictionChange earlier, RestrictionValue later) => earlier with
    {
        Rate = later.Rate ?? earlier.Rate,
        StopSell = later.StopSell ?? earlier.StopSell,
        ClosedToArrival = later.ClosedToArrival ?? earlier.ClosedToArrival,
        ClosedToDeparture = later.ClosedToDeparture ?? earlier.ClosedToDeparture,
        MinStayArrival = later.MinStayArrival ?? earlier.MinStayArrival,
        MinStayThrough = later.MinStayThrough ?? earlier.MinStayThrough,
        MaxStay = later.MaxStay ?? earlier.MaxStay,
    };

    private static DayOfWeek ToDayOfWeek(Weekday day) => day switch
    {
        Weekday.Mo => DayOfWeek.Monday,
        Weekday.Tu => DayOfWeek.Tuesday,
        Weekday.We => DayOfWeek.Wednesday,
        Weekday.Th => DayOfWeek.Thursday,
        Weekday.Fr => DayOfWeek.Friday,
        Weekday.Sa => DayOfWeek.Saturday,
        _ => DayOfWeek.Sunday,
    };
}
