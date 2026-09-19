using BookingEngine.Domain.RatePlans;

namespace BookingEngine.Application.Storefront;

public sealed record StorefrontRatePlan(Guid Id, string Code, string Name, MealPlan MealPlan);

public sealed record StorefrontRoomType(Guid Id, string Code, string Name, int MaxAdults, int MaxChildren, IReadOnlyList<StorefrontRatePlan> RatePlans);

public sealed record StorefrontPropertyResponse(
    Guid Id,
    string Name,
    string Currency,
    string Timezone,
    string CountryCode,
    string City,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    IReadOnlyList<StorefrontRoomType> RoomTypes);

public sealed record NightlyPrice(DateOnly Date, decimal Price);

public sealed record Offer(
    Guid RoomTypeId,
    string RoomTypeCode,
    string RoomTypeName,
    Guid RatePlanId,
    string RatePlanCode,
    string RatePlanName,
    MealPlan MealPlan,
    decimal TotalPrice,
    IReadOnlyList<NightlyPrice> Nightly);

public sealed record SearchResponse(
    Guid PropertyId,
    string Currency,
    DateOnly Checkin,
    DateOnly Checkout,
    int Nights,
    int Adults,
    int Children,
    IReadOnlyList<Offer> Offers);

public sealed record CalendarDay(DateOnly Date, bool Available, decimal? MinPrice);

public sealed record CalendarResponse(Guid PropertyId, string Currency, int Adults, int Children, IReadOnlyList<CalendarDay> Days);
