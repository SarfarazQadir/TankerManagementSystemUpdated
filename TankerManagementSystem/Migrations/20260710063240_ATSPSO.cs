using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TankerManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class ATSPSO : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AtsPsoEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TankerId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TankerLedgerId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtsPsoEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtsPsoEntries_Tankers_TankerId",
                        column: x => x.TankerId,
                        principalTable: "Tankers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "a00d46e2-1cc4-4bbc-9bcb-c1a11edcc7ad", "AQAAAAIAAYagAAAAEJmPqsvCAs25jkD7g7mLcKxhE7HKNK8spAYiSNnGAfL+W4Nfk5oUnRxQ6M/fKe2AhA==", "420e790b-2371-47da-8a71-746f9bb07afe" });

            migrationBuilder.CreateIndex(
                name: "IX_AtsPsoEntries_TankerId",
                table: "AtsPsoEntries",
                column: "TankerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AtsPsoEntries");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "660e295f-9667-4446-b3d7-cb65760b7dff", "AQAAAAIAAYagAAAAEP67QAcsn6/X/OuuRz7YCTRT3WptBASWs3U+w6YbqwFVOXHQMUS2b7sIp9q9M/pCiQ==", "13bc501e-d5e5-4711-9fac-a4ddb139a4a0" });
        }
    }
}
