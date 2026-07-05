using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TankerManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddKhataPersonAndLinkToCashLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PersonId",
                table: "PersonalKhatas");

            migrationBuilder.AddColumn<int>(
                name: "KhataPersonId",
                table: "PersonalKhatas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ModuleName",
                table: "PersonalKhatas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReferenceId",
                table: "PersonalKhatas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KhataPersonId",
                table: "CashLedgers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KhataPersons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhataPersons", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "660e295f-9667-4446-b3d7-cb65760b7dff", "AQAAAAIAAYagAAAAEP67QAcsn6/X/OuuRz7YCTRT3WptBASWs3U+w6YbqwFVOXHQMUS2b7sIp9q9M/pCiQ==", "13bc501e-d5e5-4711-9fac-a4ddb139a4a0" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalKhatas_KhataPersonId",
                table: "PersonalKhatas",
                column: "KhataPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgers_KhataPersonId",
                table: "CashLedgers",
                column: "KhataPersonId");

            migrationBuilder.AddForeignKey(
                name: "FK_CashLedgers_KhataPersons_KhataPersonId",
                table: "CashLedgers",
                column: "KhataPersonId",
                principalTable: "KhataPersons",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PersonalKhatas_KhataPersons_KhataPersonId",
                table: "PersonalKhatas",
                column: "KhataPersonId",
                principalTable: "KhataPersons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashLedgers_KhataPersons_KhataPersonId",
                table: "CashLedgers");

            migrationBuilder.DropForeignKey(
                name: "FK_PersonalKhatas_KhataPersons_KhataPersonId",
                table: "PersonalKhatas");

            migrationBuilder.DropTable(
                name: "KhataPersons");

            migrationBuilder.DropIndex(
                name: "IX_PersonalKhatas_KhataPersonId",
                table: "PersonalKhatas");

            migrationBuilder.DropIndex(
                name: "IX_CashLedgers_KhataPersonId",
                table: "CashLedgers");

            migrationBuilder.DropColumn(
                name: "KhataPersonId",
                table: "PersonalKhatas");

            migrationBuilder.DropColumn(
                name: "ModuleName",
                table: "PersonalKhatas");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "PersonalKhatas");

            migrationBuilder.DropColumn(
                name: "KhataPersonId",
                table: "CashLedgers");

            migrationBuilder.AddColumn<string>(
                name: "PersonId",
                table: "PersonalKhatas",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "100",
                columns: new[] { "ConcurrencyStamp", "PasswordHash", "SecurityStamp" },
                values: new object[] { "7971aade-ffa5-44b4-b0a4-48d915804e6e", "AQAAAAIAAYagAAAAEFgZvXazcS+3w4Pr0SgsnoDW4KJerADgL6zQ1svpfnV2lJNafu04ThI+MjOtsOHPyw==", "fbb460ac-09ad-46a2-a8f4-3b802c77a590" });
        }
    }
}
