using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace BookingEngine.Api.Errors;

public static class ValidationKeyNaming
{
    public static void ToSnakeCase(ProblemDetailsContext context)
    {
        if (context.ProblemDetails is not HttpValidationProblemDetails validation)
        {
            return;
        }

        var errors = validation.Errors.ToDictionary(e => Convert(e.Key), e => e.Value);
        validation.Errors.Clear();
        foreach (var (key, messages) in errors)
        {
            validation.Errors[key] = messages;
        }
    }

    private static string Convert(string key) =>
        string.Join('.', key.Split('.').Select(JsonNamingPolicy.SnakeCaseLower.ConvertName));
}
