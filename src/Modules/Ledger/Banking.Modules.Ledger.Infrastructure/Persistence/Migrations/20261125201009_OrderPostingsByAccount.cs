using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banking.Modules.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderPostingsByAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_postings_account_id",
                schema: "ledger",
                table: "postings");

            migrationBuilder.CreateIndex(
                name: "ix_postings_account_id_id",
                schema: "ledger",
                table: "postings",
                columns: new[] { "account_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_postings_account_id_id",
                schema: "ledger",
                table: "postings");

            migrationBuilder.CreateIndex(
                name: "ix_postings_account_id",
                schema: "ledger",
                table: "postings",
                column: "account_id");
        }
    }
}
