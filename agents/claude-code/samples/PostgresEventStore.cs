using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Npgsql;

namespace Project.Infrastructure.EventSourcing.Postgres;

public class PostgresEventStore : IEventStore
{
    private readonly string _connectionString;

    public PostgresEventStore(string connectionString)
    {
        _connectionString = connectionString;
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
            var type = reader.GetString(0);
            var payload = reader.GetString(1);
            // deserialization needs type mapping; returning raw payloads as JsonElement is an option
            list.Add(payload);
        }
        return list;
    }
}
