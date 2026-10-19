using System.Net;
using Npgsql;

namespace Banking.IntegrationTests;

/// <summary>
/// Proves the test infrastructure itself works: Docker is reachable, PostgreSQL starts, and the API boots.
/// </summary>
public sealed class InfrastructureSmokeTests(PostgresFixture postgres, BankingApiFactory api) : IClassFixture<BankingApiFactory>
{
    [Fact]
    public async Task Postgres_container_accepts_connections()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand("SHOW server_version", connection);
        var version = (string?)await command.ExecuteScalarAsync(cancellationToken);

        version.ShouldNotBeNull().ShouldStartWith("18.");
    }

    [Fact]
    public async Task Api_health_endpoint_reports_healthy()
    {
        using var client = api.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Each_test_class_gets_a_migrated_database_of_its_own()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(api.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        connection.Database.ShouldStartWith("test_");
        await using var command = new NpgsqlCommand("SELECT count(*) FROM ledger.accounts", connection);
        var cashAccounts = (long)(await command.ExecuteScalarAsync(cancellationToken))!;

        cashAccounts.ShouldBe(4);
    }
}
