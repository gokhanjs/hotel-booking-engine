using BookingEngine.Application.Ari;
using BookingEngine.Application.Properties;
using BookingEngine.Application.RatePlans;
using BookingEngine.Application.RoomTypes;
using BookingEngine.Application.Storefront;
using Microsoft.Extensions.DependencyInjection;

namespace BookingEngine.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) => services
        .AddScoped<AriService>()
        .AddScoped<PropertyService>()
        .AddScoped<RoomTypeService>()
        .AddScoped<RatePlanService>()
        .AddScoped<StorefrontService>();
}
