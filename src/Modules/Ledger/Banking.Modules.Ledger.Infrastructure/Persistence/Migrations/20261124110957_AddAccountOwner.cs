using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banking.Modules.Ledger.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                schema: "ledger",
                table: "accounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_owner_id",
                schema: "ledger",
                table: "accounts",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_accounts_owner_id",
                schema: "ledger",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "owner_id",
                schema: "ledger",
                table: "accounts");
        }
    }
}
