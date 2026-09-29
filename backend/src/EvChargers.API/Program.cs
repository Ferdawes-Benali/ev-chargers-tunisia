using Serilog;
using EvChargers.API.Configuration;
using EvChargers.Infrastructure;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.WriteTo.Console().ReadFrom.Configuration(ctx.Configuration));
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<EvChargers.Application.Interfaces.IStationService, EvChargers.Application.Services.StationService>();
builder.Services.AddScoped<EvChargers.Application.Interfaces.IUserService, EvChargers.Application.Services.UserService>();
builder.Services.AddScoped<EvChargers.Application.Interfaces.ICompanionService, EvChargers.Application.Services.CompanionService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<EvChargers.Application.Interfaces.IReachEstimatorService, EvChargers.Application.Services.ReachEstimatorService>();
builder.Services.AddScoped<EvChargers.Application.Interfaces.ITripPlannerService, EvChargers.Application.Services.TripPlannerService>();
builder.Services.AddValidatorsFromAssembly(typeof(EvChargers.Application.Validators.CreateStationRequestValidator).Assembly);

// Deployment: settings validated at startup (strict outside Development, see Configuration/)
builder.Services.AddProxyForwardedHeaders();
builder.Services.AddFrontendCors();
builder.Services.AddSupabaseAuthentication();
builder.Services.AddApiRateLimiting(builder.Environment);
builder.Services.AddApiHealthChecks();

builder.Services.AddAuthorization();


var app = builder.Build();
app.LogStartupSummary();

// First: the real client IP and scheme behind Render's proxy (rate limiting, HSTS and redirects depend on them)
app.UseForwardedHeaders();
app.UseMiddleware<EvChargers.API.Middleware.ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging(HostingExtensions.ConfigureRequestLogging);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EvChargers.Infrastructure.Persistence.AppDbContext>();
    await EvChargers.Infrastructure.Persistence.DbSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}
else
{
    // Render redirects HTTP to HTTPS at its edge and talks plain HTTP to the container, so no redirect here
    // (it would target a port the container doesn't serve). HSTS is only sent on requests that arrived over HTTPS.
    app.UseHsts();
}
app.UseCors(HostingExtensions.FrontendCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapApiHealthChecks();
app.Run();
