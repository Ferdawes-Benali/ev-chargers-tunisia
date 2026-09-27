using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EvChargers.Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<Persistence.AppDbContext>(opt =>
            opt.UseNpgsql(
                configuration.GetConnectionString("Default"),
                o => o.UseNetTopologySuite()));

        services.AddScoped<Application.Interfaces.IVehicleRepository, Persistence.EfVehicleRepository>();
        services.AddScoped<Application.Interfaces.IStationRepository, Persistence.EfStationRepository>();
        services.AddScoped<Application.Interfaces.IUserRepository, Persistence.EfUserRepository>();
        services.AddScoped<Application.Interfaces.IAuditLogRepository, Persistence.EfAuditLogRepository>();

        services.AddMemoryCache();
        services.AddHttpClient<Application.Interfaces.IGeocodingProvider, External.OrsGeocodingProvider>();
        services.AddHttpClient<Application.Interfaces.IRoutingProvider, External.OrsDirectionsProvider>();
        services.AddHttpClient<Application.Interfaces.IWeatherProvider, External.OpenMeteoWeatherProvider>();

        return services;
    }
}