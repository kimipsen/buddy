using Microsoft.EntityFrameworkCore;
using Project.Infrastructure.EventSourcing.Postgres;
using Project.Infrastructure.Persistence;
using Project.Infrastructure.EventSourcing;

var builder = WebApplication.CreateBuilder(args);

// configuration: use a default connection string if none provided
var conn = builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Database=kim;Username=kim;Password=kim";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<OrderDbContext>(opt => opt.UseNpgsql(conn));
builder.Services.AddSingleton<IEventStore>(sp => new PostgresEventStore(conn));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.Run();
