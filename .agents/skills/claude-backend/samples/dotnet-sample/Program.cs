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

builder.Services.AddDbContext<Project.Infrastructure.Persistence.OrderDbContext>(opt => opt.UseNpgsql(conn));
builder.Services.AddSingleton<Project.Infrastructure.EventSourcing.IEventTypeMapper>(sp => new Project.Infrastructure.EventSourcing.ReflectionEventTypeMapper());
builder.Services.AddSingleton<Project.Infrastructure.EventSourcing.IEventStore>(sp => new Project.Infrastructure.EventSourcing.Postgres.PostgresEventStore(conn, sp.GetRequiredService<Project.Infrastructure.EventSourcing.IEventTypeMapper>()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
await app.RunAsync();
