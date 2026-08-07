using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TankerManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddIsGeneralExpenseToCashLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGeneralExpense",
                table: "CashLedgers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "ca17dbb3-14c9-4422-8d6b-879ccf283ebe", "AQAAAAIAAYagAAAAEIeD25dhor+tz516uSXZ4LjRuCRmhSuwN7dsxsfLeUerRkqXSXjX/TZvFgXEfX48mQ==", "ee103f28-309e-476e-8a3a-effa3baeefff" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsGeneralExpense",
                table: "CashLedgers");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "48d68cbe-aada-4a75-92dd-8c2006376e05", "AQAAAAIAAYagAAAAEPZu6wX3lFjZ8O6nb9+QipcvSUHambmIeQxk2Qc1kDRjGdLeN3ecP1iG797pz8ed5A==", "8a12c2d9-177e-410b-a378-00207267f6a2" });
        }
    }
}
