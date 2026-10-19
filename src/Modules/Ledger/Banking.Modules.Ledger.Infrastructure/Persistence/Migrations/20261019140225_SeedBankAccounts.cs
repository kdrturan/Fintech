using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banking.Modules.Ledger.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Creates the bank's cash accounts, one per supported currency, with the fixed ids in BankAccounts.
    /// Written by hand: `dotnet ef migrations add` generated the empty class, the data is ours.
    /// </summary>
    public partial class SeedBankAccounts : Migration
    {
        private static readonly (string Id, string Currency)[] CashAccounts =
        [
            ("00000000-0000-7000-8000-000000000101", "TRY"),
            ("00000000-0000-7000-8000-000000000102", "USD"),
            ("00000000-0000-7000-8000-000000000103", "EUR"),
            ("00000000-0000-7000-8000-000000000104", "JPY"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (id, currency) in CashAccounts)
            {
                migrationBuilder.InsertData(
                    schema: "ledger",
                    table: "accounts",
                    columns: ["id", "type", "currency", "allows_negative_balance", "balance_amount", "balance_currency"],
                    values: [new Guid(id), "Asset", currency, true, 0m, currency]);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (id, _) in CashAccounts)
            {
                migrationBuilder.DeleteData(schema: "ledger", table: "accounts", keyColumn: "id", keyValue: new Guid(id));
            }
        }
    }
}
