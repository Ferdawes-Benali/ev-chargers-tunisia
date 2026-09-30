using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        services.AddScoped<Persistence.DatabaseConnectivity>();

        services.AddMemoryCache();
        services.AddHttpClient<Application.Interfaces.IGeocodingProvider, External.OrsGeocodingProvider>();
        services.AddHttpClient<Application.Interfaces.IRoutingProvider, External.OrsDirectionsProvider>();
        services.AddHttpClient<Application.Interfaces.IWeatherProvider, External.OpenMeteoWeatherProvider>();
        services.AddHttpClient<Application.Interfaces.IPlacesProvider, External.OverpassPlacesProvider>();

        services.AddCompanionWarmup();

        services.AddEmailServices();

        return services;
    }

    /// <summary>
    /// Companion places: stored per station, refreshed in the background (one shared queue, one worker),
    /// so requests only read the database.
    /// </summary>
    public static IServiceCollection AddCompanionWarmup(this IServiceCollection services)
    {
        services.AddScoped<Application.Interfaces.IPlacesCacheRepository, Persistence.EfPlacesCacheRepository>();
        services.AddScoped<Application.Interfaces.IPlacesCacheRefresher, Application.Services.PlacesCacheRefresher>();
        services.AddSingleton<Companion.ChannelPlacesRefreshQueue>();
        services.AddSingleton<Application.Interfaces.IPlacesRefreshQueue>(sp => sp.GetRequiredService<Companion.ChannelPlacesRefreshQueue>());
        services.AddHostedService<Companion.CompanionWarmupService>();

        return services;
    }

    /// <summary>
    /// Email: settings (validated at startup), one shared queue (singleton), the Resend client, and the background worker.
    /// Outside Development the app refuses to start without Email:FromAddress and Resend:ApiKey.
    /// </summary>
    public static IServiceCollection AddEmailServices(this IServiceCollection services)
    {
        services.AddOptions<Email.EmailOptions>()
            .BindConfiguration(Email.EmailOptions.SectionName)
            .PostConfigure<IHostEnvironment>((options, env) =>
            {
                if (env.IsDevelopment() && string.IsNullOrWhiteSpace(options.FromAddress))
                    options.FromAddress = Email.EmailOptions.DevelopmentFromAddress;
            })
            .Validate<IHostEnvironment>(
                (options, env) => env.IsDevelopment() || !string.IsNullOrWhiteSpace(options.FromAddress),
                "Email:FromAddress is required outside Development (set the Email__FromAddress environment variable).")
            .Validate(
                options => string.IsNullOrWhiteSpace(options.FromAddress) || IsBareAddress(options.FromAddress),
                "Email:FromAddress must be a bare address such as noreply@example.tn; put the display name in Email:FromName.")
            .ValidateOnStart();

        services.AddOptions<Email.ResendOptions>()
            .BindConfiguration(Email.ResendOptions.SectionName)
            .Validate<IHostEnvironment>(
                (options, env) => env.IsDevelopment() || !string.IsNullOrWhiteSpace(options.ApiKey),
                "Resend:ApiKey is required outside Development (set the Resend__ApiKey environment variable).")
            .ValidateOnStart();

        services.AddSingleton<Email.ChannelEmailQueue>();
        services.AddSingleton<Application.Email.IEmailQueue>(sp => sp.GetRequiredService<Email.ChannelEmailQueue>());
        services.AddHttpClient<Application.Email.IEmailSender, Email.ResendEmailSender>();
        services.AddHostedService<Email.EmailConfigurationCheck>();
        services.AddHostedService<Email.EmailDispatcher>();

        return services;
    }

    private static bool IsBareAddress(string value) =>
        MailAddress.TryCreate(value.Trim(), out var address) && address.Address == value.Trim();
}