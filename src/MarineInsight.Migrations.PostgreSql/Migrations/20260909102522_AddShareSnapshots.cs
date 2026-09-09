using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarineInsight.Migrations.PostgreSql.Migrations
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
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    link_validity_days = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "share_snapshots",
                columns: table => new
                {
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_snapshots", x => x.token);
                });

            migrationBuilder.CreateIndex(
                name: "IX_share_snapshots_expires_at",
                table: "share_snapshots",
                column: "expires_at");
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
