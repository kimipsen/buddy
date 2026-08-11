Migrations and local Postgres (Postgres 19)

1. Start local Postgres via Docker Compose:

docker compose up -d
```bash
cd .claude/claude-code/samples/dotnet-sample
docker compose up -d
```

2. Create and apply EF Core migrations (requires `dotnet-ef` tool):

```bash
dotnet tool install --global dotnet-ef
# from project folder
dotnet ef migrations add InitialCreate -p Project.Web.csproj -s Project.Web.csproj --context Project.Infrastructure.Persistence.OrderDbContext
dotnet ef database update -p Project.Web.csproj -s Project.Web.csproj --context Project.Infrastructure.Persistence.OrderDbContext
```

3. The `OrderDbContextFactory` is provided for design-time DbContext creation.

Notes:
- Connection string defaults to `Host=localhost;Database=kim;Username=kim;Password=kim`. Override with `CONNECTION` env var.
- Migrations will create the `orders` schema and tables as configured in `OrderDbContext` mapping.
