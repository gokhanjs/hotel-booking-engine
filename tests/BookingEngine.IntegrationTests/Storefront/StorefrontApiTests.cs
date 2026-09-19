using System.Net;
using System.Text.Json;
using BookingEngine.Application.Properties;
using BookingEngine.Application.RatePlans;
using BookingEngine.Application.RoomTypes;
using BookingEngine.Application.Storefront;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace BookingEngine.IntegrationTests.Storefront;

public class StorefrontApiTests(ApiFactory factory)
{
    private static readonly DateOnly _checkin = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40);
    private readonly HttpClient _management = factory.CreateManagementClient();
    private readonly HttpClient _public = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_returns_sellable_offers_cheapest_first()
    {
        var hotel = await SeedHotelAsync();

        var search = await SearchAsync(hotel.Property.Id, nights: 2, adults: 2, children: 1);

        Assert.Equal("EUR", search.Currency);
        Assert.Equal(
            [("NRF", 180m + 30m), ("BAR", 200m + 30m)],
            search.Offers.Select(o => (o.RatePlanCode, o.TotalPrice)));
        Assert.All(search.Offers, o => Assert.Equal(2, o.Nightly.Count));
    }

    [Fact]
    public async Task Single_adult_gets_the_occupancy_discount_through_the_derived_plan()
    {
        var hotel = await SeedHotelAsync();

        var search = await SearchAsync(hotel.Property.Id, nights: 1, adults: 1);

        Assert.Equal([("NRF", 72m), ("BAR", 80m)], search.Offers.Select(o => (o.RatePlanCode, o.TotalPrice)));
    }

    [Fact]
    public async Task Restrictions_remove_offers_from_search()
    {
        var hotel = await SeedHotelAsync();
        await PatchRestrictionsAsync(hotel.Property.Id, new { rate_plan_id = hotel.Derived.Id, date_from = _checkin, date_to = _checkin, min_stay_arrival = 3 });

        var twoNights = await SearchAsync(hotel.Property.Id, nights: 2);
        var threeNights = await SearchAsync(hotel.Property.Id, nights: 3);

        Assert.Equal(["BAR"], twoNights.Offers.Select(o => o.RatePlanCode));
        Assert.Equal(["NRF", "BAR"], threeNights.Offers.Select(o => o.RatePlanCode));
    }

    [Fact]
    public async Task Writes_invalidate_cached_search_results()
    {
        var hotel = await SeedHotelAsync();
        var before = await SearchAsync(hotel.Property.Id, nights: 1);

        var redis = factory.Services.GetRequiredService<IConnectionMultiplexer>();
        var keys = redis.GetServers()[0].Keys(pattern: $"property:{hotel.Property.Id}:v*:search:*").ToList();
        Assert.NotEmpty(keys);

        await PatchRestrictionsAsync(hotel.Property.Id, new { rate_plan_id = hotel.Parent.Id, date_from = _checkin, date_to = _checkin, rate = 150 });
        var after = await SearchAsync(hotel.Property.Id, nights: 1);

        Assert.Equal(100m, before.Offers.Single(o => o.RatePlanCode == "BAR").TotalPrice);
        Assert.Equal(150m, after.Offers.Single(o => o.RatePlanCode == "BAR").TotalPrice);
        Assert.Equal(135m, after.Offers.Single(o => o.RatePlanCode == "NRF").TotalPrice);
    }

    [Fact]
    public async Task Calendar_shows_lowest_nightly_price_and_closed_days()
    {
        var hotel = await SeedHotelAsync();
        await PatchRestrictionsAsync(hotel.Property.Id, new { rate_plan_id = hotel.Derived.Id, date_from = _checkin.AddDays(1), date_to = _checkin.AddDays(1), stop_sell = true });
        await PatchRestrictionsAsync(hotel.Property.Id, new { rate_plan_id = hotel.Parent.Id, date_from = _checkin.AddDays(2), date_to = _checkin.AddDays(2), stop_sell = true });
        await PatchRestrictionsAsync(hotel.Property.Id, new { rate_plan_id = hotel.Derived.Id, date_from = _checkin.AddDays(2), date_to = _checkin.AddDays(2), stop_sell = true });

        var response = await _public.GetAsync(
            $"/api/v1/storefront/properties/{hotel.Property.Id}/calendar?from={_checkin:yyyy-MM-dd}&to={_checkin.AddDays(2):yyyy-MM-dd}", Ct);
        var calendar = await response.ReadAsync<CalendarResponse>();

        Assert.Equal(
            [(true, (decimal?)90m), (true, 100m), (false, null)],
            calendar.Days.Select(d => (d.Available, d.MinPrice)));
    }

    [Fact]
    public async Task Property_details_are_public()
    {
        var hotel = await SeedHotelAsync();

        var details = await (await _public.GetAsync($"/api/v1/storefront/properties/{hotel.Property.Id}", Ct)).ReadAsync<StorefrontPropertyResponse>();

        var roomType = Assert.Single(details.RoomTypes);
        Assert.Equal(["BAR", "NRF"], roomType.RatePlans.Select(p => p.Code));
    }

    [Theory]
    [InlineData(0, 0, 2, HttpStatusCode.UnprocessableEntity)]
    [InlineData(-45, 1, 2, HttpStatusCode.UnprocessableEntity)]
    [InlineData(0, 31, 2, HttpStatusCode.UnprocessableEntity)]
    [InlineData(0, 1, 0, HttpStatusCode.BadRequest)]
    public async Task Invalid_searches_are_rejected(int checkinOffset, int nights, int adults, HttpStatusCode expected)
    {
        var hotel = await SeedHotelAsync();
        var checkin = _checkin.AddDays(checkinOffset);
        var checkout = nights == 0 ? checkin : checkin.AddDays(nights);

        var response = await _public.GetAsync(
            $"/api/v1/storefront/properties/{hotel.Property.Id}/search?checkin={checkin:yyyy-MM-dd}&checkout={checkout:yyyy-MM-dd}&adults={adults}", Ct);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task Storefront_is_rate_limited_per_client()
    {
        var hotel = await SeedHotelAsync();
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:StorefrontPermitsPerMinute", "2"));
        var client = limited.CreateClient();
        var url = $"/api/v1/storefront/properties/{hotel.Property.Id}";

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            statuses.Add((await client.GetAsync(url, Ct)).StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }

    [Fact]
    public async Task Search_keeps_working_when_redis_is_down()
    {
        var hotel = await SeedHotelAsync();
        await using var noRedis = factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Redis", "127.0.0.1:1"));
        var client = noRedis.CreateClient();

        var search = await client.GetAsync(
            $"/api/v1/storefront/properties/{hotel.Property.Id}/search?checkin={_checkin:yyyy-MM-dd}&checkout={_checkin.AddDays(1):yyyy-MM-dd}", Ct);
        var ready = await client.GetAsync("/health/ready", Ct);

        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        Assert.Equal(2, (await search.ReadAsync<SearchResponse>()).Offers.Count);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Degraded", await ready.Content.ReadAsStringAsync(Ct));
    }

    private sealed record Hotel(PropertyResponse Property, RoomTypeResponse RoomType, RatePlanResponse Parent, RatePlanResponse Derived);

    private async Task<Hotel> SeedHotelAsync()
    {
        var property = await (await _management.PostJsonAsync("/api/v1/properties", new
        {
            name = "Storefront Hotel",
            currency = "EUR",
            timezone = "Europe/Istanbul",
            country_code = "TR",
            city = "Antalya",
        })).ReadAsync<PropertyResponse>();

        var roomType = await (await _management.PostJsonAsync($"/api/v1/properties/{property.Id}/room-types", new
        {
            code = "DBL",
            name = "Double",
            count_of_rooms = 10,
            max_adults = 2,
            max_children = 1,
        })).ReadAsync<RoomTypeResponse>();

        var parent = await (await _management.PostJsonAsync($"/api/v1/properties/{property.Id}/rate-plans", new
        {
            room_type_id = roomType.Id,
            code = "BAR",
            name = "Best Available",
            sell_mode = "per_person",
            meal_plan = "breakfast",
            child_fee = 15,
            occupancies = new[] { new { adults = 1, price_adjustment = -20 }, new { adults = 2, price_adjustment = 0 } },
        })).ReadAsync<RatePlanResponse>();

        var derived = await (await _management.PostJsonAsync($"/api/v1/properties/{property.Id}/rate-plans", new
        {
            room_type_id = roomType.Id,
            code = "NRF",
            name = "Non-refundable",
            sell_mode = "per_person",
            meal_plan = "breakfast",
            child_fee = 15,
            derived = new { parent_rate_plan_id = parent.Id, type = "percent", value = -10 },
        })).ReadAsync<RatePlanResponse>();

        var until = _checkin.AddDays(10);
        await _management.PostJsonAsync($"/api/v1/properties/{property.Id}/availability", new
        {
            values = new[] { new { room_type_id = roomType.Id, date_from = _checkin, date_to = until, availability = 4 } },
        });
        await PatchRestrictionsAsync(property.Id, new { rate_plan_id = parent.Id, date_from = _checkin, date_to = until, rate = 100 });
        await PatchRestrictionsAsync(property.Id, new { rate_plan_id = derived.Id, date_from = _checkin, date_to = until, stop_sell = false });

        return new Hotel(property, roomType, parent, derived);
    }

    private async Task PatchRestrictionsAsync(Guid propertyId, object value)
    {
        var response = await _management.PostJsonAsync($"/api/v1/properties/{propertyId}/restrictions", new { values = new[] { value } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<SearchResponse> SearchAsync(Guid propertyId, int nights, int adults = 2, int children = 0)
    {
        var response = await _public.GetAsync(
            $"/api/v1/storefront/properties/{propertyId}/search?checkin={_checkin:yyyy-MM-dd}&checkout={_checkin.AddDays(nights):yyyy-MM-dd}&adults={adults}&children={children}",
            Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadAsync<SearchResponse>();
    }
}
