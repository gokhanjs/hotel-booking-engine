using System.ComponentModel.DataAnnotations;
using BookingEngine.Api.Errors;
using BookingEngine.Application.Properties;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BookingEngine.Api.Endpoints;

public static class PropertyEndpoints
{
    public static RouteGroupBuilder MapProperties(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/properties").WithTags("Properties");

        group.MapPost("/", async (CreatePropertyRequest request, PropertyService service, CancellationToken ct) =>
            {
                var property = await service.CreateAsync(request, ct);
                return TypedResults.Created($"/api/v1/properties/{property.Id}", property);
            })
            .WithName("CreateProperty")
            .WithSummary("Create a property")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/", (
                PropertyService service,
                CancellationToken ct,
                [FromQuery(Name = "page"), Range(1, int.MaxValue)] int page = 1,
                [FromQuery(Name = "page_size"), Range(1, 100)] int pageSize = 20) => service.ListAsync(page, pageSize, ct))
            .WithName("ListProperties")
            .WithSummary("List properties")
            .ProducesValidationProblem();

        group.MapGet("/{propertyId:guid}", async Task<Results<Ok<PropertyResponse>, ProblemHttpResult>> (
                Guid propertyId, PropertyService service, CancellationToken ct) =>
            {
                var result = await service.GetAsync(propertyId, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("GetProperty")
            .WithSummary("Get a property")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{propertyId:guid}", async Task<Results<Ok<PropertyResponse>, ProblemHttpResult>> (
                Guid propertyId, UpdatePropertyRequest request, PropertyService service, CancellationToken ct) =>
            {
                var result = await service.UpdateAsync(propertyId, request, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("UpdateProperty")
            .WithSummary("Update a property")
            .WithDescription("Currency is immutable because stored rates are denominated in it.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{propertyId:guid}", async Task<Results<NoContent, ProblemHttpResult>> (
                Guid propertyId, PropertyService service, CancellationToken ct) =>
            {
                var result = await service.DeleteAsync(propertyId, ct);
                return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
            })
            .WithName("DeleteProperty")
            .WithSummary("Delete a property")
            .WithDescription("Cascades to room types, rate plans and all ARI data of the property.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }
}
