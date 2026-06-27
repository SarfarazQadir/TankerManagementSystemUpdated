using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TankerManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class RolesSeeded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "84ee2ea6-5289-4e06-826f-263f335fb008", "1", "Admin", "Admin" },
                    { "a4b8aaa5-d310-4e41-ac35-90b3b237a247", "2", "User", "User" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "84ee2ea6-5289-4e06-826f-263f335fb008");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a4b8aaa5-d310-4e41-ac35-90b3b237a247");
        }
    }
}
