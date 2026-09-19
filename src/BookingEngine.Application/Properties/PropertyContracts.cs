using System.ComponentModel.DataAnnotations;
using BookingEngine.Domain.Properties;

namespace BookingEngine.Application.Properties;

public sealed record CreatePropertyRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: Required, StringLength(3, MinimumLength = 3)] string Currency,
    [property: Required, MaxLength(64)] string Timezone,
    [property: Required, StringLength(2, MinimumLength = 2)] string CountryCode,
    [property: Required, MaxLength(100)] string City,
    [property: MaxLength(500)] string? Address = null,
    [property: Range(-90, 90)] decimal? Latitude = null,
    [property: Range(-180, 180)] decimal? Longitude = null);

public sealed record UpdatePropertyRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: Required, MaxLength(64)] string Timezone,
    [property: Required, StringLength(2, MinimumLength = 2)] string CountryCode,
    [property: Required, MaxLength(100)] string City,
    [property: MaxLength(500)] string? Address = null,
    [property: Range(-90, 90)] decimal? Latitude = null,
    [property: Range(-180, 180)] decimal? Longitude = null);

public sealed record PropertyResponse(
    Guid Id,
    string Name,
    string Currency,
    string Timezone,
    string CountryCode,
    string City,
    string? Address,
    decimal? Latitude,
    decimal? Longitude,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static PropertyResponse From(Property p) =>
        new(p.Id, p.Name, p.Currency, p.Timezone, p.CountryCode, p.City, p.Address, p.Latitude, p.Longitude, p.CreatedAt, p.UpdatedAt);
}
