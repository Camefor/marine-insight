using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarineInsight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeDefaultLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_home_default",
                table: "locations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "locations",
                keyColumn: "id",
                keyValue: new Guid("70cfb8c4-7af7-4c43-8f38-9a27e7cc2de7"),
                column: "is_home_default",
                value: false);

            migrationBuilder.UpdateData(
                table: "locations",
                keyColumn: "id",
                keyValue: new Guid("8a477d67-73fa-4f43-b954-cd29d238a89d"),
                column: "is_home_default",
                value: false);

            migrationBuilder.UpdateData(
                table: "locations",
                keyColumn: "id",
                keyValue: new Guid("9b2c4d6e-8f1a-4b7c-9d3e-5f0a2c4b6d8e"),
                column: "is_home_default",
                value: false);

            migrationBuilder.UpdateData(
                table: "locations",
                keyColumn: "id",
                keyValue: new Guid("d6ac8e90-44ae-4d1f-88b9-8b73db7af6a1"),
                column: "is_home_default",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_home_default",
                table: "locations");
        }
    }
}
