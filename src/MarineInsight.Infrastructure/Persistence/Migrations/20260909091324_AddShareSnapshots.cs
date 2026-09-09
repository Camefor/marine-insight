using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarineInsight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShareSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "share_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    link_validity_days = table.Column<int>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "share_snapshots",
                columns: table => new
                {
                    token = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    retain_until = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_snapshots", x => x.token);
                });

            migrationBuilder.CreateIndex(
                name: "IX_share_snapshots_expires_at",
                table: "share_snapshots",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_share_snapshots_retain_until",
                table: "share_snapshots",
                column: "retain_until");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "share_settings");

            migrationBuilder.DropTable(
                name: "share_snapshots");
        }
    }
}
