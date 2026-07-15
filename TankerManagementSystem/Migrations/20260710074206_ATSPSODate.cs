using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TankerManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class ATSPSODate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EntryDate",
                table: "AtsPsoEntries",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "2e6b7602-b60c-489e-ac8a-89b675482371", "AQAAAAIAAYagAAAAELdKBIa7LVtmaJUCmaUvGBJmO0shUfSqZWrxXh7DCVE0wBu6MBmUJ7YLkDN8tQKEcw==", "673ba12e-900c-48c2-a1b2-c3429e786f0a" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EntryDate",
                table: "AtsPsoEntries");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "a00d46e2-1cc4-4bbc-9bcb-c1a11edcc7ad", "AQAAAAIAAYagAAAAEJmPqsvCAs25jkD7g7mLcKxhE7HKNK8spAYiSNnGAfL+W4Nfk5oUnRxQ6M/fKe2AhA==", "420e790b-2371-47da-8a71-746f9bb07afe" });
        }
    }
}
