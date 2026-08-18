using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarineInsight.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministratorRole : Migration
    {
        // 固定确定性角色 Id，供注册时 AddToRoleAsync 与授权策略共用。
        private static readonly Guid AdministratorRoleId =
            new("0f4c3f6a-6c7d-4e1f-9a2b-3c4d5e6f7081");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" },
                values: new object[] { AdministratorRoleId, "Administrator", "ADMINISTRATOR", AdministratorRoleId.ToString() });

            // 幂等补授：若 xuehaq@gmail.com 在迁移前已注册则补授角色；
            // 未注册时无操作，由注册端点按 Admin:Email 配置自动授权兜底。
            migrationBuilder.Sql(
                "INSERT INTO \"user_roles\" (\"UserId\", \"RoleId\") " +
                "SELECT u.\"Id\", '" + AdministratorRoleId + "' FROM \"users\" u " +
                "WHERE u.\"Email\" = 'xuehaq@gmail.com' " +
                "AND NOT EXISTS (SELECT 1 FROM \"user_roles\" r " +
                "WHERE r.\"UserId\" = u.\"Id\" AND r.\"RoleId\" = '" + AdministratorRoleId + "');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM \"user_roles\" WHERE \"RoleId\" = '" + AdministratorRoleId + "';");
            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "Id",
                keyValue: AdministratorRoleId);
        }
    }
}
