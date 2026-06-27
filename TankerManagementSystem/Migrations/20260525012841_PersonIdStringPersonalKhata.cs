using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TankerManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class PersonIdStringPersonalKhata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PersonId",
                table: "PersonalKhatas",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "07977281-8ab6-4ed3-964e-d1c1dc8e40c9", "AQAAAAIAAYagAAAAEM2QxoT9ZThT/m2h/v3lXSHNe6+trdHAGB2WcicONtzQwoHx4o2xgjrZvZNrK6msbA==", "cae817a3-aef4-4381-ba58-5d70d81b0ad6" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "PersonId",
                table: "PersonalKhatas",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "131a3edd-2361-4143-b081-692598674294", "AQAAAAIAAYagAAAAEGACowyAE8M7veyyTx5y4pN2l8gREAnwy3VHrY1xL0JVV+lkNlLRqMhq67bi/EnXEQ==", "9f5a0d44-204c-44f3-a3ed-e642259f02ec" });
        }
    }
}
