using System.Net;
using Npgsql;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Banking.IntegrationTests;

public sealed class InfrastructureSmokeTests(PostgresFixture postgresFixture)
{
    [Fact]
    public async Task Postgres_container_accepts_connections()
    {
        var cancelliationToken = TestContext.Current.CancellationToken;

        var connectionString = postgresFixture.ConnectionString;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancelliationToken);

        await using var command = new NpgsqlCommand("SHOW server_version;", connection);

        var result = (string?)await command.ExecuteScalarAsync(cancelliationToken);

        result.ShouldNotBeNull().ShouldStartWith("18.");
    }


    [Fact]
    public async Task Api_health_endpoint_reports_healthy()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
