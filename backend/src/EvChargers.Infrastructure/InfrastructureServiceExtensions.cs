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

        return services;
    }
}