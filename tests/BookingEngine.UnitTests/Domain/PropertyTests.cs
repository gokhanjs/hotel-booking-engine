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

    [Theory]
    [InlineData("Pacific/Kiritimati", "2026-10-06")]
    [InlineData("Europe/Istanbul", "2026-10-05")]
    [InlineData("Pacific/Pago_Pago", "2026-10-04")]
    public void Today_follows_the_property_timezone_across_the_utc_date_line(string timezone, string expected)
    {
        var property = new Property("Hotel", "EUR", timezone, "TR", "Antalya");

        Assert.Equal(DateOnly.Parse(expected), property.Today(new DateTimeOffset(2026, 10, 5, 10, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Blank_name_is_rejected()
    {
        Assert.Throws<DomainException>(() => new Property("  ", "EUR", "Europe/Istanbul", "TR", "Antalya"));
    }
}
