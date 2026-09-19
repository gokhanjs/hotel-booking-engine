using System.Text.RegularExpressions;
using BookingEngine.Domain.Common;

namespace BookingEngine.Domain.Properties;

public sealed partial class Property : Entity
{
    public string Name { get; private set; } = null!;
    public string Currency { get; private set; } = null!;
    public string Timezone { get; private set; } = null!;
    public string CountryCode { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public string? Address { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    private Property() { }

    public Property(string name, string currency, string timezone, string countryCode, string city)
    {
        Rename(name);
        Currency = ValidCode(currency, CurrencyPattern(), "Currency");
        Timezone = ValidTimezone(timezone);
        CountryCode = ValidCode(countryCode, CountryPattern(), "Country code");
        City = Guard.NotBlank(city, "City", 100);
    }

    public void Rename(string name) => Name = Guard.NotBlank(name, "Name", 200);

    public void SetLocation(string? address, decimal? latitude, decimal? longitude)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new DomainException("Coordinates are out of range.");
        }

        if (latitude.HasValue != longitude.HasValue)
        {
            throw new DomainException("Latitude and longitude must be set together.");
        }

        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        Latitude = latitude;
        Longitude = longitude;
    }

    private static string ValidCode(string value, Regex pattern, string name) =>
        pattern.IsMatch(value) ? value : throw new DomainException($"{name} '{value}' is not a valid ISO code.");

    private static string ValidTimezone(string timezone) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(timezone, out _)
            ? timezone
            : throw new DomainException($"Timezone '{timezone}' is not a valid IANA timezone.");

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyPattern();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryPattern();
}
