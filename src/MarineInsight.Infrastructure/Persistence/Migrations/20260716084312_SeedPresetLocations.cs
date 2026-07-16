using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MarineInsight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedPresetLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "locations",
                columns: new[] { "id", "coast_orientation_deg", "created_at", "display_name", "is_preset", "latitude", "location_type", "longitude", "normalized_name", "time_zone_id" },
                values: new object[,]
                {
                    { new Guid("70cfb8c4-7af7-4c43-8f38-9a27e7cc2de7"), null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "嵊泗列岛", true, 30.727m, (short)1, 122.451m, "嵊泗列岛", "Asia/Shanghai" },
                    { new Guid("8a477d67-73fa-4f43-b954-cd29d238a89d"), null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "东极岛", true, 30.194m, (short)1, 122.687m, "东极岛", "Asia/Shanghai" },
                    { new Guid("d6ac8e90-44ae-4d1f-88b9-8b73db7af6a1"), null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "普陀山", true, 30.010m, (short)1, 122.388m, "普陀山", "Asia/Shanghai" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "locations",
                keyColumn: "id",
                keyValue: new Guid("70cfb8c4-7af7-4c43-8f38-9a27e7cc2de7"));

            migrationBuilder.DeleteData(
                table: "locations",
                keyColumn: "id",
                keyValue: new Guid("8a477d67-73fa-4f43-b954-cd29d238a89d"));

            migrationBuilder.DeleteData(
                table: "locations",
                keyColumn: "id",
                keyValue: new Guid("d6ac8e90-44ae-4d1f-88b9-8b73db7af6a1"));
        }
    }
}
