using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TankerManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class openingbalancedate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ShortageLiters",
                table: "TripLedgers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpeningBalanceDate",
                table: "Tankers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "b00c65c1-2305-4bc7-9cb6-70dc682c2bbb", "AQAAAAIAAYagAAAAEBzyaPRwNgBzggdwXMQuPIshCXcmIxIQkfb2QWQs9hsdjvpldd55xajAZXT1ZyVPXg==", "e69bdbab-f017-4376-a5dc-5d8eddcacf17" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShortageLiters",
                table: "TripLedgers");

            migrationBuilder.DropColumn(
                name: "OpeningBalanceDate",
                table: "Tankers");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "2e6b7602-b60c-489e-ac8a-89b675482371", "AQAAAAIAAYagAAAAELdKBIa7LVtmaJUCmaUvGBJmO0shUfSqZWrxXh7DCVE0wBu6MBmUJ7YLkDN8tQKEcw==", "673ba12e-900c-48c2-a1b2-c3429e786f0a" });
        }
    }
}
