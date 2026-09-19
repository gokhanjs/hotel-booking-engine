using BookingEngine.Api.Errors;
using BookingEngine.Application.RatePlans;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BookingEngine.Api.Endpoints;

public static class RatePlanEndpoints
{
    public static RouteGroupBuilder MapRatePlans(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/properties/{propertyId:guid}/rate-plans").WithTags("Rate plans");

        group.MapPost("/", async Task<Results<Created<RatePlanResponse>, ProblemHttpResult>> (
                Guid propertyId, CreateRatePlanRequest request, RatePlanService service, CancellationToken ct) =>
            {
                var result = await service.CreateAsync(propertyId, request, ct);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/v1/properties/{propertyId}/rate-plans/{result.Value!.Id}", result.Value)
                    : result.Error.ToProblem();
            })
            .WithName("CreateRatePlan")
            .WithSummary("Create a rate plan")
            .WithDescription(
                "Per-person plans price each adult count relative to the daily base rate. " +
                "Derived plans take their rate from a parent plan with a percent or fixed adjustment.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/", async Task<Results<Ok<IReadOnlyList<RatePlanResponse>>, ProblemHttpResult>> (
                Guid propertyId, RatePlanService service, CancellationToken ct) =>
            {
                var result = await service.ListAsync(propertyId, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("ListRatePlans")
            .WithSummary("List rate plans of a property")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{ratePlanId:guid}", async Task<Results<Ok<RatePlanResponse>, ProblemHttpResult>> (
                Guid propertyId, Guid ratePlanId, RatePlanService service, CancellationToken ct) =>
            {
                var result = await service.GetAsync(propertyId, ratePlanId, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("GetRatePlan")
            .WithSummary("Get a rate plan")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{ratePlanId:guid}", async Task<Results<Ok<RatePlanResponse>, ProblemHttpResult>> (
                Guid propertyId, Guid ratePlanId, UpdateRatePlanRequest request, RatePlanService service, CancellationToken ct) =>
            {
                var result = await service.UpdateAsync(propertyId, ratePlanId, request, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("UpdateRatePlan")
            .WithSummary("Update a rate plan")
            .WithDescription("Code and sell mode are immutable; omitting `derived` detaches the plan from its parent.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{ratePlanId:guid}", async Task<Results<NoContent, ProblemHttpResult>> (
                Guid propertyId, Guid ratePlanId, RatePlanService service, CancellationToken ct) =>
            {
                var result = await service.DeleteAsync(propertyId, ratePlanId, ct);
                return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
            })
            .WithName("DeleteRatePlan")
            .WithSummary("Delete a rate plan")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return api;
    }
}
