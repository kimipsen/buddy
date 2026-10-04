# EF Core and hand-rolled Postgres event store

**Not used in Buddy.** Buddy persists everything through Marten (see `../SKILL.md`). Use this only for other .NET services that choose EF Core for read models or a hand-rolled event store.

## Rules

- One small `DbContext` per domain, each with its own schema (`modelBuilder.HasDefaultSchema("orders")`), so a domain can be extracted to its own service later.
- EF Core for read models only; the event stream stays the source of truth.
- Target .NET 11 with the Npgsql EF Core provider.
- Map ID value types with a value converter:
  ```csharp
  b.Property(x => x.Id).HasConversion(v => v.Value, v => new OrderId(v));
  ```
- Provide an `IDesignTimeDbContextFactory<T>` so `dotnet ef` works without running the app.
- Read the connection string from configuration or an environment variable, never a literal.

## Hand-rolled event store table

```sql
CREATE SCHEMA IF NOT EXISTS event_store;

CREATE TABLE event_store.events (
  id bigserial primary key,
  stream_name text not null,
  event_type text not null,
  event_payload jsonb not null,
  occurred_at timestamptz not null default now()
);

CREATE INDEX idx_event_store_stream ON event_store.events (stream_name, id);
```

- JSONB payloads; discriminate with the union's explicit `EventType`, never `GetType().Name` on the boxed union.
- Persist the union's payload (`e.Value`), not the union value.
- Add an expected-version check on append if concurrent writers are possible.

## Migrations

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> -p <Project>.csproj -s <Startup>.csproj --context <Namespace>.<Domain>DbContext
dotnet ef database update -p <Project>.csproj -s <Startup>.csproj --context <Namespace>.<Domain>DbContext
```

## Samples

- `../samples/OrderDbContext.cs`, `../samples/IEventStore.cs`, `../samples/PostgresEventStore.cs`, `../samples/efcore-mapping.md`.
- `../samples/dotnet-sample/` - end-to-end Domain/Infrastructure/Web layout with design-time factory, docker-compose and `MIGRATIONS-README.md`.
