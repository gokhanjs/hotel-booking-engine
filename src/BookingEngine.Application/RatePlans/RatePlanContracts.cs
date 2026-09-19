using System.ComponentModel.DataAnnotations;
using BookingEngine.Domain.RatePlans;

namespace BookingEngine.Application.RatePlans;

public sealed record OccupancyDto([property: Range(1, 20)] int Adults, decimal PriceAdjustment);

public sealed record DerivedPricingDto(Guid ParentRatePlanId, DerivedAdjustmentType Type, decimal Value);

public sealed record CreateRatePlanRequest(
    Guid RoomTypeId,
    [property: Required, MaxLength(50)] string Code,
    [property: Required, MaxLength(200)] string Name,
    SellMode SellMode,
    MealPlan MealPlan,
    [property: Range(0, 1_000_000)] decimal ChildFee,
    IReadOnlyList<OccupancyDto>? Occupancies = null,
    DerivedPricingDto? Derived = null);

public sealed record UpdateRatePlanRequest(
    [property: Required, MaxLength(200)] string Name,
    MealPlan MealPlan,
    [property: Range(0, 1_000_000)] decimal ChildFee,
    IReadOnlyList<OccupancyDto>? Occupancies = null,
    DerivedPricingDto? Derived = null);

public sealed record RatePlanResponse(
    Guid Id,
    Guid RoomTypeId,
    string Code,
    string Name,
    SellMode SellMode,
    MealPlan MealPlan,
    decimal ChildFee,
    IReadOnlyList<OccupancyDto> Occupancies,
    DerivedPricingDto? Derived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static RatePlanResponse From(RatePlan r) => new(
        r.Id,
        r.RoomTypeId,
        r.Code,
        r.Name,
        r.SellMode,
        r.MealPlan,
        r.ChildFee,
        r.Occupancies.Select(o => new OccupancyDto(o.Adults, o.PriceAdjustment)).ToList(),
        r.Derived is { } d ? new DerivedPricingDto(d.ParentRatePlanId, d.Type, d.Value) : null,
        r.CreatedAt,
        r.UpdatedAt);
}
