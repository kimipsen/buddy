using buddy.Common.Idempotency;
using buddy.Common.Postgres;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Mealplans;
using buddy.Features.Medicines;
using buddy.Features.Pickups;
using buddy.Features.WorkLocations;
using buddy.Features.Progress;
using buddy.Features.TaskLibrary;
using buddy.Features.Users;
using buddy.IntegrationTests.Fixtures;

using Marten;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Weasel.Postgresql;

using Xunit;

namespace buddy.IntegrationTests.Common.Postgres;

public sealed class PostgresDataSourceTests
{
    [Fact]
    public void Adds_the_default_cap_and_application_name_when_the_connection_string_has_none()
    {
        var result = new NpgsqlConnectionStringBuilder(
            PostgresDataSource.WithDefaults("Host=db;Port=5432;Database=postgres;Username=postgres;Password=postgres;"));

        Assert.Equal(PostgresDataSource.DefaultMaxPoolSize, result.MaxPoolSize);
        Assert.Equal(PostgresDataSource.DefaultApplicationName, result.ApplicationName);
        Assert.Equal("db", result.Host);
        Assert.Equal("postgres", result.Password);
    }

    [Theory]
    [InlineData("Host=db;Maximum Pool Size=42")]
    [InlineData("Host=db;maximum pool size=42")]
    [InlineData("Host=db;MaxPoolSize=42")]
    public void Keeps_an_explicit_cap(string connectionString)
    {
        Assert.Equal(42, new NpgsqlConnectionStringBuilder(PostgresDataSource.WithDefaults(connectionString)).MaxPoolSize);
    }

    [Theory]
    [InlineData("Host=db;Application Name=keycloak-probe")]
    [InlineData("Host=db;ApplicationName=keycloak-probe")]
    public void Keeps_an_explicit_application_name(string connectionString)
    {
        Assert.Equal("keycloak-probe", new NpgsqlConnectionStringBuilder(PostgresDataSource.WithDefaults(connectionString)).ApplicationName);
    }

    [Fact]
    public void The_cap_can_be_overridden_by_the_caller()
    {
        Assert.Equal(7, new NpgsqlConnectionStringBuilder(PostgresDataSource.WithDefaults("Host=db", maxPoolSize: 7)).MaxPoolSize);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_connection_string(string connectionString)
    {
        Assert.Throws<ArgumentException>(() => PostgresDataSource.WithDefaults(connectionString));
    }
}

// Proves the shared pool survives the real host's configuration ordering: the fixture overrides
// ConnectionStrings:Postgres with the Testcontainers string, and every Marten store must use the
// single NpgsqlDataSource built from that string (not the appsettings.json placeholder).
[Collection(BuddyApiCollection.Name)]
public sealed class PostgresDataSourceHostTests(BuddyApiFixture fixture)
{
    public static TheoryData<Type> StoreTypes =>
    [
        typeof(IUsersStore),
        typeof(IGroupsStore),
        typeof(ITaskLibraryStore),
        typeof(ICalendarsStore),
        typeof(IMedicinesStore),
        typeof(IMealplansStore),
        typeof(IPickupsStore),
        typeof(IWorkLocationsStore),
        typeof(IProgressStore),
        typeof(IIdempotencyStore)
    ];

    [Fact]
    public void The_shared_data_source_uses_the_overridden_connection_string_with_the_default_cap()
    {
        var dataSource = fixture.Host.Services.GetRequiredService<NpgsqlDataSource>();
        var parsed = new NpgsqlConnectionStringBuilder(dataSource.ConnectionString);

        Assert.NotEqual("...", parsed.Host);
        Assert.Equal(PostgresDataSource.DefaultMaxPoolSize, parsed.MaxPoolSize);
        Assert.Equal(PostgresDataSource.DefaultApplicationName, parsed.ApplicationName);
    }

    [Theory]
    [MemberData(nameof(StoreTypes))]
    public void Every_marten_store_uses_the_shared_data_source(Type storeType)
    {
        var shared = fixture.Host.Services.GetRequiredService<NpgsqlDataSource>();
        var store = (IDocumentStore)fixture.Host.Services.GetRequiredService(storeType);

        var database = Assert.IsAssignableFrom<PostgresqlDatabase>(store.Storage.Database);
        Assert.Same(shared, database.DataSource);
        Assert.False(database.OwnsDataSource);
    }
}
