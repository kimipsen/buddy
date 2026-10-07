using buddy.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace buddy.IntegrationTests.Features.Privacy;

// Reads Postgres directly -- every events table and every document table in every schema -- so an
// erasure test doesn't trust the code it is testing to say what is left.
internal static class PersonalDataScanner
{
    // "schema.table: value" for every table whose JSON still contains one of the values.
    public static async Task<IReadOnlyList<string>> FindAsync(BuddyApiFixture fixture, params string[] values)
    {
        var dataSource = fixture.Host.Services.GetRequiredService<NpgsqlDataSource>();
        await using var connection = await dataSource.OpenConnectionAsync();

        var tables = new List<(string Schema, string Table)>();
        await using (var command = new NpgsqlCommand(
            """
            select table_schema, table_name from information_schema.columns
            where column_name = 'data' and data_type = 'jsonb'
              and table_schema not in ('pg_catalog', 'information_schema')
            """,
            connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        var hits = new List<string>();

        foreach (var (schema, table) in tables)
        {
            foreach (var value in values)
            {
                await using var command = new NpgsqlCommand(
                    $"select exists (select 1 from \"{schema}\".\"{table}\" where data::text ilike '%' || $1 || '%')",
                    connection);
                command.Parameters.AddWithValue(value);

                if (await command.ExecuteScalarAsync() is true)
                {
                    hits.Add($"{schema}.{table}: {value}");
                }
            }
        }

        return hits;
    }
}
