using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Banking.Modules.Transfers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "transfers");

            migrationBuilder.CreateTable(
                name: "transfers",
                schema: "transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transfers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transfers_status",
                schema: "transfers",
                table: "transfers",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transfers",
                schema: "transfers");
        }
    }
}
