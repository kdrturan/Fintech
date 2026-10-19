using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Banking.IntegrationTests;

/// <summary>
/// The real API, in memory, on a database of its own. Used as a class fixture: one per test class.
/// The recovery job's background loop is off; tests run a recovery round themselves when they need one.
/// </summary>
public sealed class BankingApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync() => ConnectionString = await postgres.CreateDatabaseAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Banking", ConnectionString);
    }

    async ValueTask IAsyncDisposable.DisposeAsync() => await base.DisposeAsync();
}
