using BookingEngine.Domain.Common;
using BookingEngine.Domain.RatePlans;

namespace BookingEngine.UnitTests.Domain;

public class DerivedPricingTests
{
    [Theory]
    [InlineData(DerivedAdjustmentType.Percent, -10, 100, 90)]
    [InlineData(DerivedAdjustmentType.Percent, 12.5, 80, 90)]
    [InlineData(DerivedAdjustmentType.Amount, 15.5, 100, 115.5)]
    [InlineData(DerivedAdjustmentType.Amount, -30, 100, 70)]
    [InlineData(DerivedAdjustmentType.Percent, -33.33, 10.01, 6.67)]
    public void Apply_adjusts_parent_rate(DerivedAdjustmentType type, decimal value, decimal parentRate, decimal expected)
    {
        var pricing = new DerivedPricing(Guid.NewGuid(), type, value);

        Assert.Equal(expected, pricing.Apply(parentRate));
    }

    [Fact]
    public void Apply_never_goes_below_zero()
    {
        var pricing = new DerivedPricing(Guid.NewGuid(), DerivedAdjustmentType.Amount, -500);

        Assert.Equal(0, pricing.Apply(100));
    }

    [Fact]
    public void Adjustments_with_more_than_two_decimals_are_rejected()
    {
        Assert.Throws<DomainException>(() => new DerivedPricing(Guid.NewGuid(), DerivedAdjustmentType.Percent, -33.333m));
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(-150)]
    public void Percent_at_or_below_minus_hundred_is_rejected(decimal value)
    {
        Assert.Throws<DomainException>(() => new DerivedPricing(Guid.NewGuid(), DerivedAdjustmentType.Percent, value));
    }
}
