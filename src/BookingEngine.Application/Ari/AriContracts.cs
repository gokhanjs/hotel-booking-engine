using System.ComponentModel.DataAnnotations;

namespace BookingEngine.Application.Ari;

public enum Weekday
{
    Mo,
    Tu,
    We,
    Th,
    Fr,
    Sa,
    Su,
}

public sealed record AvailabilityValue(
    Guid RoomTypeId,
    DateOnly DateFrom,
    DateOnly DateTo,
    [property: Range(0, 10_000)] int Availability,
    IReadOnlyList<Weekday>? Days = null);

public sealed record AvailabilityUpdateRequest([property: Required, MinLength(1), MaxLength(500)] IReadOnlyList<AvailabilityValue> Values);

public sealed record RestrictionValue(
    Guid RatePlanId,
    DateOnly DateFrom,
    DateOnly DateTo,
    IReadOnlyList<Weekday>? Days = null,
    [property: Range(0, 10_000_000)] decimal? Rate = null,
    bool? StopSell = null,
    bool? ClosedToArrival = null,
    bool? ClosedToDeparture = null,
    [property: Range(1, 365)] int? MinStayArrival = null,
    [property: Range(1, 365)] int? MinStayThrough = null,
    [property: Range(0, 365)] int? MaxStay = null);

public sealed record RestrictionUpdateRequest([property: Required, MinLength(1), MaxLength(500)] IReadOnlyList<RestrictionValue> Values);

public sealed record AriUpdateResponse(int AffectedDates);

public sealed record AvailabilityResponse(Guid RoomTypeId, DateOnly Date, int Availability);

public sealed record RestrictionResponse(
    Guid RatePlanId,
    DateOnly Date,
    decimal? Rate,
    bool StopSell,
    bool ClosedToArrival,
    bool ClosedToDeparture,
    int MinStayArrival,
    int MinStayThrough,
    int? MaxStay);
