using Serilog;
using EvChargers.Infrastructure;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.WriteTo.Console().ReadFrom.Configuration(ctx.Configuration));
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<EvChargers.Application.Interfaces.IStationService, EvChargers.Application.Services.StationService>();
builder.Services.AddValidatorsFromAssembly(typeof(EvChargers.Application.Validators.CreateStationRequestValidator).Assembly);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});


var app = builder.Build();
app.UseMiddleware<EvChargers.API.Middleware.ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EvChargers.Infrastructure.Persistence.AppDbContext>();
    await EvChargers.Infrastructure.Persistence.DbSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowFrontendDev");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();