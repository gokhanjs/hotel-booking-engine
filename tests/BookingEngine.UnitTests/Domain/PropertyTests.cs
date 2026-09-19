using BookingEngine.Domain.Common;
using BookingEngine.Domain.Properties;

namespace BookingEngine.UnitTests.Domain;

public class PropertyTests
{
    [Theory]
    [InlineData("eur", "Europe/Istanbul", "TR")]
    [InlineData("EURO", "Europe/Istanbul", "TR")]
    [InlineData("EUR", "Mars/Olympus", "TR")]
    [InlineData("EUR", "Europe/Istanbul", "TUR")]
    public void Invalid_codes_are_rejected(string currency, string timezone, string country)
    {
        Assert.Throws<DomainException>(() => new Property("Hotel", currency, timezone, country, "Antalya"));
    }

    [Fact]
    public void Coordinates_must_be_set_together()
    {
        var property = new Property("Hotel", "EUR", "Europe/Istanbul", "TR", "Antalya");

        Assert.Throws<DomainException>(() => property.SetLocation(null, 36.88m, null));
    }

    [Fact]
    public void Blank_name_is_rejected()
    {
        Assert.Throws<DomainException>(() => new Property("  ", "EUR", "Europe/Istanbul", "TR", "Antalya"));
    }
}
