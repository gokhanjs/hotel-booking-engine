using System.Net;
using System.Text.Json;
using BookingEngine.Application.Common;
using BookingEngine.Application.Properties;
using BookingEngine.Application.RatePlans;
using BookingEngine.Application.RoomTypes;

namespace BookingEngine.IntegrationTests.Management;

public class ManagementApiTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateManagementClient();

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task Management_endpoints_require_a_valid_api_key(string? key)
    {
        var client = factory.CreateClient();
        if (key is not null)
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", key);
        }

        var response = await client.GetAsync("/api/v1/properties", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/openapi/v1.json")]
    [InlineData("/docs")]
    [InlineData("/health/live")]
    public async Task Documentation_and_liveness_are_public(string url)
    {
        var response = await factory.CreateClient().GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Property_lifecycle()
    {
        var created = await _client.PostJsonAsync("/api/v1/properties", NewProperty("Lifecycle Hotel"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var property = await created.ReadAsync<PropertyResponse>();
        Assert.Equal($"/api/v1/properties/{property.Id}", created.Headers.Location!.OriginalString);

        var updated = await _client.PutJsonAsync($"/api/v1/properties/{property.Id}", new
        {
            name = "Renamed Hotel",
            timezone = "Europe/Berlin",
            country_code = "DE",
            city = "Berlin",
        });
        Assert.Equal("Renamed Hotel", (await updated.ReadAsync<PropertyResponse>()).Name);

        var deleted = await _client.DeleteAsync($"/api/v1/properties/{property.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var missing = await _client.GetAsync($"/api/v1/properties/{property.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Properties_are_listed_with_paging_meta()
    {
        await _client.PostJsonAsync("/api/v1/properties", NewProperty("Paged Hotel"));

        var response = await _client.GetAsync("/api/v1/properties?page=1&page_size=1", TestContext.Current.CancellationToken);
        var page = await response.ReadAsync<PagedResponse<PropertyResponse>>();

        Assert.Single(page.Data);
        Assert.Equal(1, page.Meta.PageSize);
        Assert.True(page.Meta.Total >= 1);
    }

    [Fact]
    public async Task Invalid_request_shape_returns_validation_problem()
    {
        var response = await _client.PostJsonAsync("/api/v1/properties", new { currency = "EURO", timezone = "UTC", country_code = "TR", city = "X" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.ReadAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal(["currency", "name"], errors.EnumerateObject().Select(e => e.Name).Order());
    }

    [Fact]
    public async Task Nested_validation_errors_use_snake_case_paths()
    {
        var property = await CreatePropertyAsync();
        var roomType = await CreateRoomTypeAsync(property.Id, "DBL");

        var response = await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/rate-plans", new
        {
            room_type_id = roomType.Id,
            code = "BAR",
            name = "Best",
            sell_mode = "per_person",
            meal_plan = "breakfast",
            child_fee = 0,
            occupancies = new[] { new { adults = 0, price_adjustment = 0 } },
        });

        var errors = (await response.ReadAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("occupancies[0].adults", Assert.Single(errors.EnumerateObject()).Name);
    }

    [Fact]
    public async Task Numbers_sent_as_strings_are_rejected()
    {
        var property = await CreatePropertyAsync();

        var response = await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/room-types", new
        {
            code = "DBL",
            name = "Double",
            count_of_rooms = "10",
            max_adults = 2,
            max_children = 0,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Domain_rule_violations_return_unprocessable_entity()
    {
        var response = await _client.PostJsonAsync("/api/v1/properties", NewProperty("Bad Zone", timezone: "Mars/Olympus"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("domain.rule_violation", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Duplicate_room_type_code_is_a_conflict()
    {
        var property = await CreatePropertyAsync();
        await CreateRoomTypeAsync(property.Id, "DBL");

        var response = await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/room-types", RoomTypeBody("dbl"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("room_type.code_taken", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Per_person_rate_plan_requires_occupancies()
    {
        var property = await CreatePropertyAsync();
        var roomType = await CreateRoomTypeAsync(property.Id, "DBL");

        var response = await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/rate-plans", new
        {
            room_type_id = roomType.Id,
            code = "BAR",
            name = "Best Available",
            sell_mode = "per_person",
            meal_plan = "breakfast",
            child_fee = 20,
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("rate_plan.occupancies_required", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Derived_rate_plan_lifecycle_protects_parents()
    {
        var property = await CreatePropertyAsync();
        var roomType = await CreateRoomTypeAsync(property.Id, "DBL");
        var parent = await CreatePerPersonPlanAsync(property.Id, roomType.Id, "BAR");

        var childResponse = await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/rate-plans", new
        {
            room_type_id = roomType.Id,
            code = "NRF",
            name = "Non-refundable",
            sell_mode = "per_person",
            meal_plan = "breakfast",
            child_fee = 20,
            derived = new { parent_rate_plan_id = parent.Id, type = "percent", value = -10 },
        });
        Assert.Equal(HttpStatusCode.Created, childResponse.StatusCode);
        var child = await childResponse.ReadAsync<RatePlanResponse>();
        Assert.Equal(parent.Id, child.Derived!.ParentRatePlanId);

        var deleteParent = await _client.DeleteAsync($"/api/v1/properties/{property.Id}/rate-plans/{parent.Id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, deleteParent.StatusCode);

        var deriveParent = await _client.PutJsonAsync($"/api/v1/properties/{property.Id}/rate-plans/{parent.Id}", new
        {
            name = "Best Available",
            meal_plan = "breakfast",
            child_fee = 20,
            derived = new { parent_rate_plan_id = child.Id, type = "amount", value = 5 },
        });
        Assert.Equal(HttpStatusCode.Conflict, deriveParent.StatusCode);
    }

    [Fact]
    public async Task Parent_rate_plan_must_belong_to_the_same_property()
    {
        var otherProperty = await CreatePropertyAsync();
        var otherRoomType = await CreateRoomTypeAsync(otherProperty.Id, "DBL");
        var foreignParent = await CreatePerPersonPlanAsync(otherProperty.Id, otherRoomType.Id, "BAR");

        var property = await CreatePropertyAsync();
        var roomType = await CreateRoomTypeAsync(property.Id, "DBL");

        var response = await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/rate-plans", new
        {
            room_type_id = roomType.Id,
            code = "NRF",
            name = "Non-refundable",
            sell_mode = "per_person",
            meal_plan = "breakfast",
            child_fee = 0,
            derived = new { parent_rate_plan_id = foreignParent.Id, type = "percent", value = -10 },
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("rate_plan.parent_not_found", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Room_type_capacity_cannot_drop_below_priced_occupancy()
    {
        var property = await CreatePropertyAsync();
        var roomType = await CreateRoomTypeAsync(property.Id, "TRP", maxAdults: 3);
        await CreatePerPersonPlanAsync(property.Id, roomType.Id, "BAR", adults: 3);

        var response = await _client.PutJsonAsync($"/api/v1/properties/{property.Id}/room-types/{roomType.Id}", new
        {
            name = "Triple",
            count_of_rooms = 5,
            max_adults = 2,
            max_children = 0,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("room_type.max_adults_in_use", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Room_type_can_be_deleted_with_its_rate_plans()
    {
        var property = await CreatePropertyAsync();
        var roomType = await CreateRoomTypeAsync(property.Id, "DBL");
        var plan = await CreatePerPersonPlanAsync(property.Id, roomType.Id, "BAR");

        var deleted = await _client.DeleteAsync($"/api/v1/properties/{property.Id}/room-types/{roomType.Id}", TestContext.Current.CancellationToken);
        var planAfter = await _client.GetAsync($"/api/v1/properties/{property.Id}/rate-plans/{plan.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, planAfter.StatusCode);
    }

    [Fact]
    public async Task Room_type_with_plans_derived_from_elsewhere_cannot_be_deleted()
    {
        var property = await CreatePropertyAsync();
        var parentRoom = await CreateRoomTypeAsync(property.Id, "DBL");
        var childRoom = await CreateRoomTypeAsync(property.Id, "SGL");
        var parent = await CreatePerPersonPlanAsync(property.Id, parentRoom.Id, "BAR");
        await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/rate-plans", new
        {
            room_type_id = childRoom.Id,
            code = "BAR",
            name = "Derived",
            sell_mode = "per_person",
            meal_plan = "breakfast",
            child_fee = 0,
            derived = new { parent_rate_plan_id = parent.Id, type = "amount", value = -20 },
        });

        var response = await _client.DeleteAsync($"/api/v1/properties/{property.Id}/room-types/{parentRoom.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("room_type.has_derived_dependents", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Money_values_with_more_than_two_decimals_are_rejected()
    {
        var property = await CreatePropertyAsync();
        var roomType = await CreateRoomTypeAsync(property.Id, "DBL");

        var response = await _client.PostJsonAsync($"/api/v1/properties/{property.Id}/rate-plans", new
        {
            room_type_id = roomType.Id,
            code = "FLEX",
            name = "Flexible",
            sell_mode = "per_room",
            meal_plan = "room_only",
            child_fee = 10.555m,
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Resources_of_another_property_are_not_found()
    {
        var owner = await CreatePropertyAsync();
        var roomType = await CreateRoomTypeAsync(owner.Id, "DBL");
        var stranger = await CreatePropertyAsync();

        var response = await _client.GetAsync($"/api/v1/properties/{stranger.Id}/room-types/{roomType.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static object NewProperty(string name, string timezone = "Europe/Istanbul") => new
    {
        name,
        currency = "EUR",
        timezone,
        country_code = "TR",
        city = "Antalya",
        latitude = 36.8969m,
        longitude = 30.7133m,
    };

    private static object RoomTypeBody(string code, int maxAdults = 2) => new
    {
        code,
        name = "Room " + code,
        count_of_rooms = 10,
        max_adults = maxAdults,
        max_children = 1,
    };

    private async Task<PropertyResponse> CreatePropertyAsync() =>
        await (await _client.PostJsonAsync("/api/v1/properties", NewProperty("Test Hotel"))).ReadAsync<PropertyResponse>();

    private async Task<RoomTypeResponse> CreateRoomTypeAsync(Guid propertyId, string code, int maxAdults = 2) =>
        await (await _client.PostJsonAsync($"/api/v1/properties/{propertyId}/room-types", RoomTypeBody(code, maxAdults))).ReadAsync<RoomTypeResponse>();

    private async Task<RatePlanResponse> CreatePerPersonPlanAsync(Guid propertyId, Guid roomTypeId, string code, int adults = 2)
    {
        var response = await _client.PostJsonAsync($"/api/v1/properties/{propertyId}/rate-plans", new
        {
            room_type_id = roomTypeId,
            code,
            name = "Plan " + code,
            sell_mode = "per_person",
            meal_plan = "breakfast",
            child_fee = 20,
            occupancies = Enumerable.Range(1, adults).Select(a => new { adults = a, price_adjustment = (a - adults) * 20 }),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<RatePlanResponse>();
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response) =>
        (await response.ReadAsync<JsonElement>()).GetProperty("code").GetString();
}
