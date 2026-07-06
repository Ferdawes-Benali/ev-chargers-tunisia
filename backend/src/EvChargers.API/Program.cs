using EvChargers.Infrastructure;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<EvChargers.Application.Interfaces.IStationService, EvChargers.Application.Services.StationService>();

var app = builder.Build();

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

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();