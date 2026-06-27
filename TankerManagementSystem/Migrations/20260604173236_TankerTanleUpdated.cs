using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TankerManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class TankerTanleUpdated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PreviousBalance",
                table: "Tankers",
                newName: "CurrentBalance");

            migrationBuilder.CreateTable(
                name: "TankerLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TankerId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReferenceId = table.Column<int>(type: "int", nullable: true),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RunningBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TankerLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TankerLedgers_Tankers_TankerId",
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
                values: new object[] { "fa2e9193-aae4-417c-8a57-3c71e4b3b4f1", "AQAAAAIAAYagAAAAEMwz9aiyWZIKyf+puacYxkOY0Toin86hcuVCxSgO2zdBnN6rK69q9XzOGcC024GmVQ==", "bbf6d684-3857-4c75-9069-4675d58ed873" });

            migrationBuilder.CreateIndex(
                name: "IX_TankerLedgers_TankerId",
                table: "TankerLedgers",
                column: "TankerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TankerLedgers");

            migrationBuilder.RenameColumn(
                name: "CurrentBalance",
                table: "Tankers",
                newName: "PreviousBalance");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "07977281-8ab6-4ed3-964e-d1c1dc8e40c9", "AQAAAAIAAYagAAAAEM2QxoT9ZThT/m2h/v3lXSHNe6+trdHAGB2WcicONtzQwoHx4o2xgjrZvZNrK6msbA==", "cae817a3-aef4-4381-ba58-5d70d81b0ad6" });
        }
    }
}
