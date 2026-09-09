using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarineInsight.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddShareRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "retain_until",
                table: "share_snapshots",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_share_snapshots_retain_until",
                table: "share_snapshots",
                column: "retain_until");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_share_snapshots_retain_until",
                table: "share_snapshots");

            migrationBuilder.DropColumn(
                name: "retain_until",
                table: "share_snapshots");
        }
    }
}
