using BookingEngine.Domain.Common;

namespace BookingEngine.Domain.RatePlans;

public sealed record DerivedPricing
{
    public Guid ParentRatePlanId { get; }
    public DerivedAdjustmentType Type { get; }
    public decimal Value { get; }

    public DerivedPricing(Guid parentRatePlanId, DerivedAdjustmentType type, decimal value)
    {
        if (type == DerivedAdjustmentType.Percent && value <= -100)
        {
            throw new DomainException("Percent adjustment must be greater than -100.");
        }

        ParentRatePlanId = parentRatePlanId;
        Type = type;
        Value = Guard.TwoDecimals(value, "Derived adjustment");
    }

    public decimal Apply(decimal parentRate)
    {
        var rate = Type == DerivedAdjustmentType.Percent
            ? parentRate * (1 + (Value / 100m))
            : parentRate + Value;

        return Math.Max(0, Math.Round(rate, 2, MidpointRounding.AwayFromZero));
    }
}
