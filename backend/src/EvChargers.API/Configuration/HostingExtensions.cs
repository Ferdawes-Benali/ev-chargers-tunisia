using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog.AspNetCore;
using Serilog.Events;
using EvChargers.API.Health;
using EvChargers.Infrastructure.Email;

namespace EvChargers.API.Configuration;

/// <summary>Deployment settings (CORS, auth, proxy, health, logging), validated at startup like the email settings.</summary>
public static class HostingExtensions
{
    public const string FrontendCorsPolicy = "Frontend";

    /// <summary>Only the configured frontend origins; any header and method; no credentials (bearer tokens, not cookies).</summary>
    public static IServiceCollection AddFrontendCors(this IServiceCollection services)
    {
        services.AddOptions<CorsOriginsOptions>()
            .BindConfiguration(CorsOriginsOptions.SectionName)
            .PostConfigure<IHostEnvironment>((options, env) =>
            {
                options.AllowedOrigins = (options.AllowedOrigins ?? []).Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();
                if (env.IsDevelopment() && options.AllowedOrigins.Length == 0)
                    options.AllowedOrigins = [CorsOriginsOptions.DevelopmentOrigin];
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<CorsOriginsOptions>, CorsOriginsOptionsValidator>();

        services.AddCors();
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<CorsOriginsOptions>>((cors, origins) =>
                cors.AddPolicy(FrontendCorsPolicy, policy => policy
                    .WithOrigins(origins.Value.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()));

        return services;
    }

    /// <summary>Supabase-issued JWTs; the authority comes from Supabase:Url, the audience stays "authenticated".</summary>
    public static IServiceCollection AddSupabaseAuthentication(this IServiceCollection services)
    {
        services.AddOptions<SupabaseOptions>()
            .BindConfiguration(SupabaseOptions.SectionName)
            .PostConfigure<IHostEnvironment>((options, env) =>
            {
                if (env.IsDevelopment() && string.IsNullOrWhiteSpace(options.Url))
                    options.Url = SupabaseOptions.DevelopmentUrl;
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SupabaseOptions>, SupabaseOptionsValidator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<SupabaseOptions>>((jwt, supabase) =>
            {
                jwt.Authority = supabase.Value.Authority;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudience = "authenticated",
                };
            });

        return services;
    }

    /// <summary>
    /// Render terminates TLS and forwards plain HTTP with X-Forwarded-For / X-Forwarded-Proto.
    /// Its proxy addresses are not fixed or published, so the known networks/proxies lists are cleared
    /// (by default only loopback is trusted, and the headers would be ignored). This is safe because the
    /// container is only reachable through Render's proxy, and ForwardLimit = 1 reads only the last entry,
    /// the one added by that proxy, so a client can't pick its own rate-limit key by sending the header itself.
    /// </summary>
    public static IServiceCollection AddProxyForwardedHeaders(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

    /// <summary>/health: the process is up. /health/ready: it can also reach the database (503 otherwise).</summary>
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: [DatabaseHealthCheck.ReadyTag]);
        return services;
    }

    public static WebApplication MapApiHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false, // liveness only: no dependency checks
            ResponseWriter = WriteHealthStatus,
        }).AllowAnonymous();
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(DatabaseHealthCheck.ReadyTag),
            ResponseWriter = WriteHealthStatus,
        }).AllowAnonymous();
        return app;
    }

    /// <summary>{"status":"ok"} or {"status":"unavailable"}; never the failure details.</summary>
    private static Task WriteHealthStatus(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var status = report.Status == HealthStatus.Healthy ? "ok" : "unavailable";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { status }));
    }

    /// <summary>Health probes run every few seconds: keep them out of the Information-level request log.</summary>
    public static void ConfigureRequestLogging(RequestLoggingOptions options) =>
        options.GetLevel = (context, _, exception) =>
            exception is not null || context.Response.StatusCode >= 500
                ? ApiRateLimiting.IsHealthPath(context.Request.Path) ? LogEventLevel.Warning : LogEventLevel.Error
                : ApiRateLimiting.IsHealthPath(context.Request.Path) ? LogEventLevel.Verbose : LogEventLevel.Information;

    /// <summary>
    /// One line at startup describing the deployment. Keys are reported as present or not, never their values.
    /// Reading the options here also validates them before the app does anything else.
    /// </summary>
    public static void LogStartupSummary(this WebApplication app)
    {
        var cors = app.Services.GetRequiredService<IOptions<CorsOriginsOptions>>().Value;
        var resend = app.Services.GetRequiredService<IOptions<ResendOptions>>().Value;
        var orsKeyConfigured = !string.IsNullOrWhiteSpace(app.Configuration["OrsApiKey"]);

        app.Logger.LogInformation(
            "Starting EV Chargers API. Environment: {Environment}. CORS origins: {CorsOrigins}. ORS key configured: {OrsKeyConfigured}. Resend key configured: {ResendKeyConfigured}.",
            app.Environment.EnvironmentName,
            string.Join(", ", cors.AllowedOrigins),
            orsKeyConfigured,
            !string.IsNullOrWhiteSpace(resend.ApiKey));
    }
}
