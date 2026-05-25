using Npgsql;
using Testcontainers.PostgreSql;

namespace MMORPG.Api.Tests.Fixtures;

public class PostgreSqlContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("ethereal_dreams_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var schemaScript = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "database-schema.sql"));

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(schemaScript, connection);
        await cmd.ExecuteNonQueryAsync();

        // When the connection opens, Npgsql caches the type catalog before our
        // CREATE TYPE statements run. ReloadTypesAsync re-queries pg_type on this
        // open connection and writes the result back into the global
        // NpgsqlDatabaseInfo cache keyed by host:port/db. ClearAllPools then
        // evicts any pooled connections so every subsequent open picks up the
        // refreshed catalog (which now includes character_class, item_rarity, etc.).
        await connection.ReloadTypesAsync();
        NpgsqlConnection.ClearAllPools();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
