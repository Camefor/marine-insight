using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarineInsight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePresetLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "locations",
                keyColumn: "id",
                keyValue: new Guid("8a477d67-73fa-4f43-b954-cd29d238a89d"),
                columns: new[] { "latitude", "longitude" },
                values: new object[] { 30.200m, 122.680m });

            migrationBuilder.InsertData(
                table: "locations",
                columns: new[] { "id", "coast_orientation_deg", "created_at", "display_name", "is_preset", "latitude", "location_type", "longitude", "normalized_name", "time_zone_id" },
                values: new object[] { new Guid("9b2c4d6e-8f1a-4b7c-9d3e-5f0a2c4b6d8e"), null, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "岱山岛", true, 30.288m, (short)1, 122.165m, "岱山岛", "Asia/Shanghai" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "locations",
                keyColumn: "id",
                keyValue: new Guid("9b2c4d6e-8f1a-4b7c-9d3e-5f0a2c4b6d8e"));

            migrationBuilder.UpdateData(
                table: "locations",
                keyColumn: "id",
                keyValue: new Guid("8a477d67-73fa-4f43-b954-cd29d238a89d"),
                columns: new[] { "latitude", "longitude" },
                values: new object[] { 30.194m, 122.687m });
        }
    }
}
