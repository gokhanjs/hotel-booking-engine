using System.ComponentModel.DataAnnotations;
using BookingEngine.Api.Errors;
using BookingEngine.Application.Storefront;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BookingEngine.Api.Endpoints;

public static class StorefrontEndpoints
{
    public const string RateLimitPolicy = "storefront";

    public static RouteGroupBuilder MapStorefront(this RouteGroupBuilder storefront)
    {
        var group = storefront.MapGroup("/properties/{propertyId:guid}").WithTags("Storefront");

        group.MapGet("/", async Task<Results<Ok<StorefrontPropertyResponse>, ProblemHttpResult>> (
                Guid propertyId, StorefrontService service, CancellationToken ct) =>
            {
                var result = await service.GetPropertyAsync(propertyId, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("GetStorefrontProperty")
            .WithSummary("Public property details with room types and rate plans")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/search", async Task<Results<Ok<SearchResponse>, ProblemHttpResult>> (
                Guid propertyId,
                [FromQuery(Name = "checkin")] DateOnly checkin,
                [FromQuery(Name = "checkout")] DateOnly checkout,
                StorefrontService service,
                CancellationToken ct,
                [FromQuery(Name = "adults"), Range(1, 20)] int adults = 2,
                [FromQuery(Name = "children"), Range(0, 10)] int children = 0) =>
            {
                var result = await service.SearchAsync(propertyId, checkin, checkout, adults, children, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("SearchAvailability")
            .WithSummary("Search sellable room and rate offers for a stay")
            .WithDescription(
                "Returns every room type and rate plan combination that can be sold for the whole stay, cheapest first. " +
                "Availability, stop sell, closed to arrival/departure, minimum/maximum stay and occupancy pricing are applied.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/calendar", async Task<Results<Ok<CalendarResponse>, ProblemHttpResult>> (
                Guid propertyId,
                [FromQuery(Name = "from")] DateOnly from,
                [FromQuery(Name = "to")] DateOnly to,
                StorefrontService service,
                CancellationToken ct,
                [FromQuery(Name = "adults"), Range(1, 20)] int adults = 2,
                [FromQuery(Name = "children"), Range(0, 10)] int children = 0) =>
            {
                var result = await service.CalendarAsync(propertyId, from, to, adults, children, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("GetPriceCalendar")
            .WithSummary("Lowest nightly price per date")
            .WithDescription("Indicative one-night prices; length-of-stay and arrival/departure rules are applied at search time.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return storefront;
    }
}
