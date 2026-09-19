using BookingEngine.Application.Common;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BookingEngine.Api.Errors;

public static class ProblemMapping
{
    public static ProblemHttpResult ToProblem(this Error error) => TypedResults.Problem(
        statusCode: error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity,
        },
        detail: error.Message,
        extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
