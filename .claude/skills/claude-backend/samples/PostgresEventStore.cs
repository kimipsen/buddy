using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Npgsql;

namespace Project.Infrastructure.EventSourcing.Postgres;

public class PostgresEventStore : IEventStore
{
    private readonly string _connectionString;
    private readonly Dictionary<string, System.Type> _typeMap;

    public PostgresEventStore(string connectionString)
    {
        _connectionString = connectionString;
        // Register known event types here. In a real app, use reflection or a DI-registered mapper.
        _typeMap = new Dictionary<string, System.Type>
        {
            { "OrderCreated", typeof(Project.Domain.Orders.Events.OrderEvent.OrderCreated) },
            { "ItemAdded", typeof(Project.Domain.Orders.Events.OrderEvent.ItemAdded) },
            { "OrderCompleted", typeof(Project.Domain.Orders.Events.OrderEvent.OrderCompleted) }
        };
    }

    public async Task AppendEventsAsync(string streamName, IEnumerable<object> events, Guid expectedVersion = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();
        foreach (var @event in events)
        {
            var payload = JsonSerializer.Serialize(@event);
            var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO event_store.events (stream_name, event_type, event_payload, occurred_at) VALUES (@s, @t, @p, @o)";
            cmd.Parameters.AddWithValue("@s", streamName);
            cmd.Parameters.AddWithValue("@t", @event.GetType().Name);
            cmd.Parameters.AddWithValue("@p", payload);
            cmd.Parameters.AddWithValue("@o", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }

    public async Task<IEnumerable<object>> LoadEventsAsync(string streamName)
    {
        var list = new List<object>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT event_type, event_payload FROM event_store.events WHERE stream_name = @s ORDER BY id";
        cmd.Parameters.AddWithValue("@s", streamName);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var typeName = reader.GetString(0);
            var payload = reader.GetString(1);

            if (_typeMap.TryGetValue(typeName, out var t))
            {
                try
                {
                    var obj = JsonSerializer.Deserialize(payload, t);
                    if (obj != null) list.Add(obj);
                    else list.Add(payload);
                }
                catch
                {
                    list.Add(payload);
                }
            }
            else
            {
                // Unknown type: return raw JSON payload
                list.Add(payload);
            }
        }
        return list;
    }
}
