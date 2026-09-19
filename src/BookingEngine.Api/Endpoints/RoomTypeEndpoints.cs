using BookingEngine.Api.Errors;
using BookingEngine.Application.RoomTypes;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BookingEngine.Api.Endpoints;

public static class RoomTypeEndpoints
{
    public static RouteGroupBuilder MapRoomTypes(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/properties/{propertyId:guid}/room-types").WithTags("Room types");

        group.MapPost("/", async Task<Results<Created<RoomTypeResponse>, ProblemHttpResult>> (
                Guid propertyId, CreateRoomTypeRequest request, RoomTypeService service, CancellationToken ct) =>
            {
                var result = await service.CreateAsync(propertyId, request, ct);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/v1/properties/{propertyId}/room-types/{result.Value!.Id}", result.Value)
                    : result.Error.ToProblem();
            })
            .WithName("CreateRoomType")
            .WithSummary("Create a room type")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async Task<Results<Ok<IReadOnlyList<RoomTypeResponse>>, ProblemHttpResult>> (
                Guid propertyId, RoomTypeService service, CancellationToken ct) =>
            {
                var result = await service.ListAsync(propertyId, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("ListRoomTypes")
            .WithSummary("List room types of a property")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{roomTypeId:guid}", async Task<Results<Ok<RoomTypeResponse>, ProblemHttpResult>> (
                Guid propertyId, Guid roomTypeId, RoomTypeService service, CancellationToken ct) =>
            {
                var result = await service.GetAsync(propertyId, roomTypeId, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("GetRoomType")
            .WithSummary("Get a room type")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{roomTypeId:guid}", async Task<Results<Ok<RoomTypeResponse>, ProblemHttpResult>> (
                Guid propertyId, Guid roomTypeId, UpdateRoomTypeRequest request, RoomTypeService service, CancellationToken ct) =>
            {
                var result = await service.UpdateAsync(propertyId, roomTypeId, request, ct);
                return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error.ToProblem();
            })
            .WithName("UpdateRoomType")
            .WithSummary("Update a room type")
            .WithDescription("The code is immutable because channels map inventory by it.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{roomTypeId:guid}", async Task<Results<NoContent, ProblemHttpResult>> (
                Guid propertyId, Guid roomTypeId, RoomTypeService service, CancellationToken ct) =>
            {
                var result = await service.DeleteAsync(propertyId, roomTypeId, ct);
                return result.IsSuccess ? TypedResults.NoContent() : result.Error.ToProblem();
            })
            .WithName("DeleteRoomType")
            .WithSummary("Delete a room type")
            .WithDescription("Cascades to its rate plans and ARI data.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return api;
    }
}
