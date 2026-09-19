using BookingEngine.Api.Errors;
using BookingEngine.Application.Ari;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BookingEngine.Api.Endpoints;

public static class AriEndpoints
{
    public static RouteGroupBuilder MapAri(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/properties/{propertyId:guid}").WithTags("Availability, rates and restrictions");

        group.MapPost("/availability", async Task<Results<Ok<AriUpdateResponse>, ProblemHttpResult>> (
                Guid propertyId, AvailabilityUpdateRequest request, AriService service, CancellationToken ct) =>
            {
                var result = await service.UpdateAvailabilityAsync(propertyId, request, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("UpdateAvailability")
            .WithSummary("Set room type availability for date ranges")
            .WithDescription(
                "Each value sets the sellable room count of a room type for every date in [date_from, date_to], " +
                "optionally limited to weekdays. Later values win when ranges overlap.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/availability", async Task<Results<Ok<IReadOnlyList<AvailabilityResponse>>, ProblemHttpResult>> (
                Guid propertyId,
                [FromQuery(Name = "date_from")] DateOnly dateFrom,
                [FromQuery(Name = "date_to")] DateOnly dateTo,
                AriService service,
                CancellationToken ct) =>
            {
                var result = await service.GetAvailabilityAsync(propertyId, dateFrom, dateTo, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("GetAvailability")
            .WithSummary("Read stored availability")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/restrictions", async Task<Results<Ok<AriUpdateResponse>, ProblemHttpResult>> (
                Guid propertyId, RestrictionUpdateRequest request, AriService service, CancellationToken ct) =>
            {
                var result = await service.UpdateRestrictionsAsync(propertyId, request, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("UpdateRestrictions")
            .WithSummary("Patch rates and restrictions for date ranges")
            .WithDescription(
                "Only the fields present in a value are changed; omitted fields keep their stored value. " +
                "New dates default to open with a minimum stay of one night. Send max_stay 0 to remove a maximum stay.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/restrictions", async Task<Results<Ok<IReadOnlyList<RestrictionResponse>>, ProblemHttpResult>> (
                Guid propertyId,
                [FromQuery(Name = "date_from")] DateOnly dateFrom,
                [FromQuery(Name = "date_to")] DateOnly dateTo,
                AriService service,
                CancellationToken ct) =>
            {
                var result = await service.GetRestrictionsAsync(propertyId, dateFrom, dateTo, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("GetRestrictions")
            .WithSummary("Read stored rates and restrictions")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return api;
    }
}
