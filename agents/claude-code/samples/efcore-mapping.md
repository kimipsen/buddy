EF Core mapping notes (PostgreSQL, schema-per-domain)

- Use `modelBuilder.HasDefaultSchema("orders")` to place the `Order` read models into the `orders` schema.
- Map ID value types with `HasConversion` (store Guid or string in DB):

```csharp
b.Property(x => x.Id).HasConversion(v => v.Value, v => new OrderId(v));
```

- Create separate DbContexts per domain to keep models small and migrations scoped. Example: `OrderDbContext` for orders, `InventoryDbContext` for inventory.
- For event store, create an `event_store` schema and a table like:

```sql
CREATE SCHEMA IF NOT EXISTS event_store;

CREATE TABLE event_store.events (
  id bigserial primary key,
  stream_name text not null,
  event_type text not null,
  event_payload jsonb not null,
  occurred_at timestamptz not null default now()
);
```

- Consider adding a composite index on `stream_name, id` for read performance.
```sql
CREATE INDEX idx_event_store_stream ON event_store.events (stream_name, id);
```

- Use JSONB for payloads to allow flexible event schemas and indexing.
```sql
ALTER TABLE event_store.events ALTER COLUMN event_payload SET DATA TYPE jsonb USING event_payload::jsonb;
```
``` 
*** End Patch