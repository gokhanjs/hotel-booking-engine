using System.Net;
using System.Text.Json;
using BookingEngine.Application.Ari;
using BookingEngine.Application.Properties;
using BookingEngine.Application.RatePlans;
using BookingEngine.Application.RoomTypes;

namespace BookingEngine.IntegrationTests.Management;

public class AriApiTests(ApiFactory factory)
{
    private static readonly DateOnly _start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
    private readonly HttpClient _client = factory.CreateManagementClient();

    [Fact]
    public async Task Availability_is_upserted_and_read_back()
    {
        var (property, roomType, _) = await SetupAsync();

        var first = await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/availability", new
        {
            values = new[] { new { room_type_id = roomType.Id, date_from = _start, date_to = _start.AddDays(6), availability = 5 } },
        });
        Assert.Equal(7, (await first.ReadAsync<AriUpdateResponse>()).AffectedDates);

        await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/availability", new
        {
            values = new[]
            {
                new { room_type_id = roomType.Id, date_from = _start, date_to = _start.AddDays(6), availability = 2, days = new[] { "sa", "su" } },
            },
        });

        var stored = await ReadAvailabilityAsync(property.Id);
        Assert.Equal(7, stored.Count);
        Assert.All(stored, a => Assert.Equal(a.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 2 : 5, a.Availability));
    }

    [Fact]
    public async Task Restriction_updates_only_touch_sent_fields()
    {
        var (property, _, ratePlan) = await SetupAsync();

        await PostRestrictionsAsync(property.Id, new { rate_plan_id = ratePlan.Id, date_from = _start, date_to = _start.AddDays(1), rate = 100, min_stay_arrival = 2, max_stay = 7 });
        await PostRestrictionsAsync(property.Id, new { rate_plan_id = ratePlan.Id, date_from = _start, date_to = _start, stop_sell = true, max_stay = 0 });

        var stored = await ReadRestrictionsAsync(property.Id);
        var first = stored.Single(r => r.Date == _start);
        var second = stored.Single(r => r.Date == _start.AddDays(1));

        Assert.Equal((100m, 2, true, (int?)null), (first.Rate!.Value, first.MinStayArrival, first.StopSell, first.MaxStay));
        Assert.Equal((100m, 2, false, (int?)7), (second.Rate!.Value, second.MinStayArrival, second.StopSell, second.MaxStay));
    }

    [Fact]
    public async Task New_restriction_rows_start_open()
    {
        var (property, _, ratePlan) = await SetupAsync();

        await PostRestrictionsAsync(property.Id, new { rate_plan_id = ratePlan.Id, date_from = _start, date_to = _start, closed_to_arrival = true });

        var row = Assert.Single(await ReadRestrictionsAsync(property.Id));
        Assert.Null(row.Rate);
        Assert.Equal((false, true, false, 1, 1), (row.StopSell, row.ClosedToArrival, row.ClosedToDeparture, row.MinStayArrival, row.MinStayThrough));
    }

    [Theory]
    [InlineData(11, 0, "ari.availability_exceeds_rooms")]
    [InlineData(1, -40, "ari.outside_window")]
    [InlineData(1, 800, "ari.outside_window")]
    public async Task Invalid_availability_updates_are_rejected(int availability, int startOffsetDays, string code)
    {
        var (property, roomType, _) = await SetupAsync();
        var from = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(startOffsetDays);

        var response = await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/availability", new
        {
            values = new[] { new { room_type_id = roomType.Id, date_from = from, date_to = from, availability } },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Room_types_of_other_properties_are_rejected()
    {
        var (property, _, _) = await SetupAsync();
        var (_, foreignRoomType, _) = await SetupAsync();

        var response = await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/availability", new
        {
            values = new[] { new { room_type_id = foreignRoomType.Id, date_from = _start, date_to = _start, availability = 1 } },
        });

        Assert.Equal("ari.unknown_room_type", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Derived_plans_cannot_receive_rates_but_accept_restrictions()
    {
        var (property, roomType, parent) = await SetupAsync();
        var child = await (await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/rate-plans", new
        {
            room_type_id = roomType.Id,
            code = "NRF",
            name = "Non-refundable",
            sell_mode = "per_room",
            meal_plan = "room_only",
            child_fee = 0,
            derived = new { parent_rate_plan_id = parent.Id, type = "percent", value = -10 },
        })).ReadAsync<RatePlanResponse>();

        var withRate = await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/restrictions", new
        {
            values = new[] { new { rate_plan_id = child.Id, date_from = _start, date_to = _start, rate = 90 } },
        });
        Assert.Equal("ari.rate_on_derived_plan", await ProblemCodeAsync(withRate));

        var withRestriction = await PostRestrictionsAsync(property.Id, new { rate_plan_id = child.Id, date_from = _start, date_to = _start, stop_sell = true });
        Assert.Equal(HttpStatusCode.OK, withRestriction.StatusCode);
    }

    [Fact]
    public async Task Concurrent_bulk_writes_to_the_same_dates_all_succeed()
    {
        var (property, _, ratePlan) = await SetupAsync();

        var responses = await Task.WhenAll(Enumerable.Range(1, 10).Select(i => PostRestrictionsAsync(property.Id, new
        {
            rate_plan_id = ratePlan.Id,
            date_from = _start,
            date_to = _start.AddDays(60),
            rate = 100 + i,
        })));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Single((await ReadRestrictionsAsync(property.Id)).Select(r => r.Rate).Distinct());
    }

    [Fact]
    public async Task Empty_restriction_values_are_rejected()
    {
        var (property, _, ratePlan) = await SetupAsync();

        var response = await PostRestrictionsAsync(property.Id, new { rate_plan_id = ratePlan.Id, date_from = _start, date_to = _start });

        Assert.Equal("ari.empty_update", await ProblemCodeAsync(response));
    }

    private async Task<(PropertyResponse Property, RoomTypeResponse RoomType, RatePlanResponse RatePlan)> SetupAsync()
    {
        var property = await (await _client.PostJsonAsync("/api/v1/properties", new
        {
            name = "ARI Hotel",
            currency = "EUR",
            timezone = "Europe/Istanbul",
            country_code = "TR",
            city = "Antalya",
        })).ReadAsync<PropertyResponse>();

        var roomType = await (await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/room-types", new
        {
            code = "DBL",
            name = "Double",
            count_of_rooms = 10,
            max_adults = 2,
            max_children = 1,
        })).ReadAsync<RoomTypeResponse>();

        var ratePlan = await (await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/rate-plans", new
        {
            room_type_id = roomType.Id,
            code = "FLEX",
            name = "Flexible",
            sell_mode = "per_room",
            meal_plan = "room_only",
            child_fee = 0,
        })).ReadAsync<RatePlanResponse>();

        return (property, roomType, ratePlan);
    }

    private Task<HttpResponseMessage> PostRestrictionsAsync(Guid propertyId, object value) =>
        _client.PostJsonAsync($"/api/v1/properties/{propertyId}/restrictions", new { values = new[] { value } });

    private async Task<IReadOnlyList<AvailabilityResponse>> ReadAvailabilityAsync(Guid propertyId) =>
        await (await _client.GetAsync(
            $"/api/v1/properties/{propertyId}/availability?date_from={_start:yyyy-MM-dd}&date_to={_start.AddDays(30):yyyy-MM-dd}",
            TestContext.Current.CancellationToken)).ReadAsync<IReadOnlyList<AvailabilityResponse>>();

    private async Task<IReadOnlyList<RestrictionResponse>> ReadRestrictionsAsync(Guid propertyId) =>
        await (await _client.GetAsync(
            $"/api/v1/properties/{propertyId}/restrictions?date_from={_start:yyyy-MM-dd}&date_to={_start.AddDays(30):yyyy-MM-dd}",
            TestContext.Current.CancellationToken)).ReadAsync<IReadOnlyList<RestrictionResponse>>();

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.ReadAsync<JsonElement>()).GetProperty("code").GetString();
}
