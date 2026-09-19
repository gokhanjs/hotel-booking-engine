using System.Text.Json;
using System.Text.Json.Serialization;
using BookingEngine.Api.Auth;
using BookingEngine.Api.Endpoints;
using BookingEngine.Api.Errors;
using BookingEngine.Api.OpenApi;
using BookingEngine.Application;
using BookingEngine.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ValidationKeyNaming.ToSnakeCase);
builder.Services.AddExceptionHandler<KnownExceptionHandler>();
builder.Services.AddValidation();

builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
builder.Services.AddOptions<ApiKeyAuthenticationOptions>(ApiKeyAuthenticationHandler.SchemeName).BindConfiguration("Management");
builder.Services.AddAuthorization();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Booking Engine API";
        document.Info.Description = "Property setup, pricing and availability for hotel distribution.";
        return Task.CompletedTask;
    });
    options.AddDocumentTransformer<ApiKeySecurityTransformer>();
    options.AddOperationTransformer<ApiKeySecurityTransformer>();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference("/docs", options => options
    .WithTitle("Booking Engine API")
    .AddPreferredSecuritySchemes(ApiKeyAuthenticationHandler.SchemeName));

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.MapGroup("/api/v1")
    .RequireAuthorization()
    .MapProperties()
    .MapRoomTypes()
    .MapRatePlans()
    .MapAri();

app.Run();
