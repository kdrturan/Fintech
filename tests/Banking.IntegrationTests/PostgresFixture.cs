using Banking.Modules.Ledger.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Banking.IntegrationTests.PostgresFixture))]

namespace Banking.IntegrationTests;

/// <summary>
/// One disposable PostgreSQL container shared by every test in the assembly. Migrations run once, into a
/// template database; every test class then gets its own copy of it (<see cref="CreateDatabaseAsync"/>),
/// so classes can run in parallel without seeing each other's data. See docs/adr/0016.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string TemplateDatabase = "banking_template";

    // The concurrency tests open many connections at once; the default limit of 100 is shared by all classes.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithCommand("-c", "max_connections=300")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        await ExecuteAsync($"CREATE DATABASE {TemplateDatabase}");
        var template = ConnectionStringFor(TemplateDatabase);

        var services = new ServiceCollection()
            .AddLedgerModule(template)
            .BuildServiceProvider();

        await using (services)
        {
            await services.MigrateLedgerDatabaseAsync(CancellationToken.None);
        }

        // PostgreSQL refuses to copy a database that has open connections, and the pool keeps them open.
        NpgsqlConnection.ClearAllPools();
    }

    /// <summary>A fresh database with every migration applied, copied from the template in about 100 ms.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"test_{Guid.NewGuid():N}";
        await ExecuteAsync($"CREATE DATABASE {name} TEMPLATE {TemplateDatabase}");
        return ConnectionStringFor(name);
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(ConnectionString) { Database = database }.ConnectionString;

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
